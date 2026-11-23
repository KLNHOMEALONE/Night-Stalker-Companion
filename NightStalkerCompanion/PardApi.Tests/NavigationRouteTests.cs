using PardApi;

namespace PardApi.Tests;

/// <summary>
/// Navigation between screens. What is under test is the decision of where a
/// gesture leads, not the animation that carries the user there: the app builds
/// Shell route strings out of <see cref="Routes"/>, and Shell itself is not
/// available in a plain unit test.
///
/// The requirement that produced this file: from the reticle screen the user must
/// get back to the start screen, and one tap must be enough however deep the
/// pushed stack happens to be.
/// </summary>
public class NavigationRouteTests
{
    [Fact]
    public void Home_is_an_absolute_named_route_so_one_call_lands_on_the_start_screen()
    {
        // A relative "../" would need to be repeated once per pushed page, and the
        // page count is not knowable here: a user may reach the reticle from the
        // device tab, and later from a group of the same tab after other pushes.
        // "///" addresses a named page from the root, so depth stops mattering —
        // the idiom the Innowise.Music reference app navigates with.
        Assert.Equal("///DevicePage", Routes.Home);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void Home_does_not_depend_on_how_deep_the_stack_is(int pushedPages)
    {
        Assert.Equal(Routes.Home, Routes.HomeFrom(pushedPages));
    }

    [Theory]
    [InlineData("DevicePage")]
    [InlineData("GalleryPage")]
    [InlineData("BallisticsPage")]
    public void Any_screen_is_reachable_by_name_from_anywhere(string page)
    {
        // Navigation between screens is absolute and named everywhere in the app, so
        // a link does not break when someone pushes one more screen in between.
        Assert.Equal($"///{page}", Routes.Go(page));
    }

    [Fact]
    public void Back_goes_up_exactly_one_level()
    {
        Assert.Equal("../", Routes.Back);
    }

    [Fact]
    public void Home_is_offered_only_when_it_is_somewhere_to_go()
    {
        // The start screen has no business carrying a "home" button; a control that
        // navigates the user to the page they are on reads as a broken button.
        Assert.False(Routes.HomeIsAvailable(onStartScreen: true));
        Assert.True(Routes.HomeIsAvailable(onStartScreen: false));
    }

    [Fact]
    public void Reticle_carries_its_group_in_the_query()
    {
        // Opening a group's reticle reads that group's reticle, and reading it means
        // selecting it on the device, so the group number cannot be lost on the way.
        Assert.Equal("reticle?group=2", Routes.Reticle(2));
    }
}
