#!/usr/bin/env python3
"""A stand-in for the PARD Night Stalker 4K scope, for running the app without one.

The scope is an access point at 192.168.1.254, so a simulator (or a Mac on the office
Wi-Fi) cannot reach it, and every screen then falls back to the saved snapshot and says
so. That fallback is the honest behaviour of the app, and it is also the end of what can
be tested without hardware: nothing writes, so nothing can be watched changing.

This serves the five commands the app polls and the two it writes, with the semantics
that were measured on the real device and recorded in notes/APP_FIELDS.md:

  * 6009 answers for one group at a time, and names it in GroupId;
  * GroupId is the group index A..E, the same counter as BallisticData in 6019;
  * par= is NOT the group: 6009&par=0..4 and 6019&par=0..4 answered byte-for-byte
    identically, so this server answers them identically. An app that starts relying on
    par has to be caught here, not on hardware;
  * writing BallisticData switches the active group, and writing GroupId does the same
    for the reticle, which is why the app puts BallisticData last and reads back.

The per-group reticles are the ones the device was measured to hold on 22.11 (capture
hunt-261122-194311): A style 6, B style 4 with PosX=-3 PosY=5, C style 3, D and E style 1
dark. So a screen drawn from here looks like a real scope's, not like a random one.

    python3 tools/mock_scope.py --port 8080

The app is pointed at it with a debug-only host (see MauiProgram); a release build never
asks, and talks to 192.168.1.254 only.
"""

import argparse
import json
import re
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlparse

# Recorded from the device (capture hunt-261122-194311, gid0..gid4_6009.xml).
RETICLES = {
    0: {"GroupId": 0, "PosX": 0, "PosY": 0, "Style": 6, "Color": 1},
    1: {"GroupId": 1, "PosX": -3, "PosY": 5, "Style": 4, "Color": 1},
    2: {"GroupId": 2, "PosX": 0, "PosY": 0, "Style": 3, "Color": 1},
    3: {"GroupId": 3, "PosX": 0, "PosY": 0, "Style": 1, "Color": 0},
    4: {"GroupId": 4, "PosX": 0, "PosY": 0, "Style": 1, "Color": 0},
}

# Recorded 6019 reply (base_6019.xml). BallisticData is filled in per reply: it is the
# active group, not a constant.
PROFILE = {
    "DistanceSwitch": 0,
    "DistanceUnit": 1,
    "BallisticData": 0,
    "Velocity": 285,
    "BulletWt": 3.00,
    "BulletBC": 0.465,
    "Altitude": 1220,
    "Temperature": 20,
    "ScopeHt": 70.1,
    "ZeroRange": 41,
    "Sharp": 0,
    "Color": 0,
}

FIRMWARE = "PARD_Night Stalker 4K_EN_20241212"

# A short 3014 batch. The real reply is 34 pairs; the app reads none of them yet, and a
# shorter answer keeps the log readable.
STATUS_BATCH = [(1002, 3), (2016, 0), (2001, 0), (2002, 5), (2020, 50), (3007, 0)]

RETICLE_FIELDS = ("Style", "Color", "PosX", "PosY")


class Scope:
    """The device's whole state: five groups' reticles and one active group."""

    def __init__(self):
        self.lock = threading.Lock()
        self.active = 0
        self.reticles = {g: dict(r) for g, r in RETICLES.items()}
        self.profile = dict(PROFILE)
        self.writes = 0

    def reticle_json(self):
        with self.lock:
            return json.dumps(self.reticles[self.active], separators=(",", ":"))

    def profile_json(self):
        with self.lock:
            self.profile["BallisticData"] = self.active
            return json.dumps(self.profile, separators=(",", ":"))

    def write_field(self, params):
        """cmd=6018: one profile field, and BallisticData among them selects the group."""
        with self.lock:
            self.writes += 1
            for field, values in params.items():
                if field in ("custom", "cmd", "par"):
                    continue
                value = int(float(values[0]))
                if field == "BallisticData" and 0 <= value <= 4:
                    self.active = value
                else:
                    self.profile[field] = value

    def write_reticle(self, params):
        """cmd=6001: GroupId picks the group, and the reticle has to arrive whole.

        Measured behaviour this copies: a 6001 carrying only some of the reticle fields is
        answered with <Status>0</Status> and stores nothing (notes/APP_FIELDS.md: that status
        confirms only that the HTTP worked), while the stock app always sends the reticle
        together — its request was seen as cmd=6001&par=0&Style=4&Color=1. A client that sends
        one changed field therefore gets a green light and an unchanged scope, which is the
        failure this server exists to reproduce.

        Writing GroupId is itself a switch — measured, and the reason the app reads back the
        group it thinks it wrote.
        """
        with self.lock:
            self.writes += 1
            group = self.active
            if "GroupId" in params:
                wanted = int(float(params["GroupId"][0]))
                if 0 <= wanted <= 4:
                    group = wanted
                    self.active = wanted
            if not all(f in params for f in RETICLE_FIELDS):
                return  # accepted, stored nowhere: what the scope does with a partial reticle
            for field in RETICLE_FIELDS:
                self.reticles[group][field] = int(float(params[field][0]))
            self.reticles[group]["GroupId"] = group


def envelope(cmd, status=0, body=""):
    return (
        '<?xml version="1.0" encoding="UTF-8" ?>\n<Function>\n'
        f"<Cmd>{cmd}</Cmd>\n<Status>{status}</Status>\n"
        + (f"<String>{body}</String>\n" if body else "")
        + "</Function>\n"
    )


def status_batch_body():
    return "".join(f"<Cmd>{c}</Cmd><Status>{s}</Status>" for c, s in STATUS_BATCH)


def serve(scope, port):
    class Handler(BaseHTTPRequestHandler):
        protocol_version = "HTTP/1.1"

        def log_message(self, *args):
            pass  # The app polls four commands per screen entry; silence is the log.

        def do_GET(self):
            query = parse_qs(urlparse(self.path).query)
            cmd = int(query.get("cmd", ["0"])[0])
            if self.path.startswith("/__state"):
                return self.reply(json.dumps(
                    {"active": scope.active, "reticles": scope.reticles,
                     "writes": scope.writes}, separators=(",", ":")))
            if cmd == 3012:
                return self.reply(envelope(3012, body=FIRMWARE))
            if cmd == 6019:
                return self.reply(envelope(6019, body=scope.profile_json()))
            if cmd == 6009:
                return self.reply(envelope(6009, body=scope.reticle_json()))
            if cmd == 3014:
                return self.reply(status_batch_body())
            if cmd == 6018:
                scope.write_field(query)
                return self.reply(envelope(6018))
            if cmd == 6001:
                scope.write_reticle(query)
                return self.reply(envelope(6001))
            # Unknown command: the real scope answers with an envelope too, so that the
            # app cannot tell "not implemented here" apart from "the scope refused".
            return self.reply(envelope(cmd, status=-1))

        def reply(self, body):
            payload = body.encode()
            self.send_response(200)
            self.send_header("Content-Type", "text/xml; charset=utf-8")
            self.send_header("Content-Length", str(len(payload)))
            self.end_headers()
            self.wfile.write(payload)

    server = ThreadingHTTPServer(("127.0.0.1", port), Handler)
    server.allow_reuse_address = True
    return server


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=8080)
    parser.add_argument("--group", type=int, default=0, help="group active at start (0..4)")
    args = parser.parse_args()

    scope = Scope()
    scope.active = args.group if 0 <= args.group <= 4 else 0
    print(f"mock scope on http://127.0.0.1:{args.port} , active group {args.group}", flush=True)
    serve(scope, args.port).serve_forever()


if __name__ == "__main__":
    main()
