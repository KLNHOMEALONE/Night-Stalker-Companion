// Генератор превью прицельных марок: крупный план перекрестия вместо общего кадра.
//
// Исходники ret1..6.png — светлые волосяные линии на тёмном поле 168×126 / 168×63 /
// 168×67. В тайле 84×84 (бокс рисунка ≈74×55) они ужимаются до 0.44×, линия в пиксель
// становится субпиксельной, а из-за разных пропорций холста марки 2 и 4 рисуются вдвое
// мельче остальных и их нельзя сравнить глазами.
//
// Работаем так: берётся окно фиксированной стороны (одинаковое во всех исходниках —
// только так масштаб общий) и рисуется так, чтобы focus-точка попала в ЦЕНТР выхода.
// Кадр рисуется целиком со сдвигом, а не вырезается прямоугольником: у ret2 и ret4
// перекрестие у самого верхнего края, и опустить его к середине квадрата прямоугольником
// внутри кадра невозможно — приходится оставлять сверху пустое поле.
//
// Эталон лежит в tools/reticle_orig (снимок HEAD), рабочие файлы — в ресурсах приложения.
// Повторный запуск поверх собственного результата запрещён проверкой по ширине.
//
//   arch -arm64 DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
//     swift tools/make_reticle_previews.swift
import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

let repo = "/Users/leanidkarpushenka/Projects/PARD_API/"
let origDir = repo + "tools/reticle_orig/"
let outDir = repo + "NightStalkerCompanion/NightStalkerCompanion/Resources/Images/"
let names = ["ret1.png", "ret2.png", "ret3.png", "ret4.png", "ret5.png", "ret6.png"]

let side: CGFloat = 55   // сторона окна в пикселях исходника; ×4 до 224, линия не плашка
let out = 224            // плотность тайла 74 pt @3x ≈ 222 px

// Куда целиться центром выхода, в долях ширины/высоты исходника.
// ret2 и ret4: верх их кадра (0) должен оказаться посередине квадрата.
let focus: [String: (CGFloat, CGFloat)] = [
    "ret1.png": (0.5, 0.5),
    "ret2.png": (0.5, 0.0),
    "ret3.png": (0.5, 0.5),
    "ret4.png": (0.5, 0.0),
    "ret5.png": (0.5, 0.5),
    "ret6.png": (0.5, 0.5),
]

let cs = CGColorSpaceCreateDeviceRGB()
let bmp = CGImageAlphaInfo.premultipliedLast.rawValue

func load(_ p: String) -> CGImage? {
    guard let s = CGImageSourceCreateWithURL(URL(fileURLWithPath: p) as CFURL, nil) else { return nil }
    return CGImageSourceCreateImageAtIndex(s, 0, nil)
}
func save(_ img: CGImage, _ p: String) {
    guard let d = CGImageDestinationCreateWithURL(URL(fileURLWithPath: p) as CFURL,
                                                 UTType.png.identifier as CFString, 1, nil) else { return }
    CGImageDestinationAddImage(d, img, nil)
    CGImageDestinationFinalize(d)
}
func montage(_ sets: [[CGImage]], to path: String) {
    let tw = 74, th = 55, gap = 10, cols = 3
    let rows = sets.count * 2                       // по три плитки на набор
    let sw = cols * tw + (cols + 1) * gap, sh = rows * th + (rows + 1) * gap
    guard let sheet = CGContext(data: nil, width: sw, height: sh, bitsPerComponent: 8, bytesPerRow: 0,
                                space: cs, bitmapInfo: bmp) else { return }
    sheet.setFillColor(CGColor(red: 0.043, green: 0.047, blue: 0.055, alpha: 1))
    sheet.fill(CGRect(x: 0, y: 0, width: sw, height: sh))
    for (r, set) in sets.enumerated() {
        for (n, img) in set.enumerated() {
            let idx = r * set.count + n
            let cell = CGRect(x: CGFloat(gap + (n % cols) * (tw + gap)),
                              y: CGFloat(sh - gap - th - (idx / cols) * (th + gap)),
                              width: CGFloat(tw), height: CGFloat(th))
            sheet.setFillColor(CGColor(red: 0.067, green: 0.075, blue: 0.086, alpha: 1))
            sheet.fill(cell)
            let s = min(Double(tw) / Double(img.width), Double(th) / Double(img.height))
            let dw = CGFloat(Double(img.width) * s), dh = CGFloat(Double(img.height) * s)
            sheet.interpolationQuality = .high
            sheet.draw(img, in: CGRect(x: cell.midX - dw / 2, y: cell.midY - dh / 2, width: dw, height: dh))
        }
    }
    guard let o = sheet.makeImage(),
          let d = CGImageDestinationCreateWithURL(URL(fileURLWithPath: path) as CFURL,
                                                  UTType.png.identifier as CFString, 1, nil) else { return }
    CGImageDestinationAddImage(d, o, nil)
    CGImageDestinationFinalize(d)
    print("монтаж: \(path)")
}

var before = [CGImage](), after = [CGImage]()
for name in names {
    guard let src = load(origDir + name) else {
        print("\(name): ЭТАЛОН не найден в \(origDir) — доставь из git, поверх результата не работаем")
        continue
    }
    guard src.width < out else {
        print("\(name): ЭТАЛОН \(src.width)x\(src.height) шире результата — это уже обработанный файл, пропускаю")
        continue
    }
    before.append(src)

    let f = focus[name] ?? (0.5, 0.5)
    let s = CGFloat(out) / side                                   // сколько раз увеличиваем кадр
    let dest = CGRect(x: CGFloat(out) / 2 - CGFloat(src.width) * f.0 * s,
                      y: CGFloat(out) / 2 - CGFloat(src.height) * (1 - f.1) * s,
                      width: CGFloat(src.width) * s, height: CGFloat(src.height) * s)
    guard let ctx = CGContext(data: nil, width: out, height: out, bitsPerComponent: 8, bytesPerRow: 0,
                              space: cs, bitmapInfo: bmp) else { continue }
    ctx.interpolationQuality = .high
    ctx.draw(src, in: dest)                                        // всё, что вышло за холст, обрезается
    guard let made = ctx.makeImage() else { continue }

    save(made, outDir + name)
    after.append(made)
    print(String(format: "%@: фокус (%.2f,%.2f) → центр, окно %d px, увеличение ×%.1f",
                 name, f.0, f.1, Int(side), s))
}

if before.count == 6 && after.count == 6 { montage([before, after], to: "/tmp/compare.png") }
