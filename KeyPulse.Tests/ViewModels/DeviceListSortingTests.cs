using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using KeyPulse.Models;

namespace KeyPulse.Tests.ViewModels;

/// <summary>
/// Exercises the device-list sorting the DeviceListViewModel relies on. The full view-model is
/// WPF/service-bound and not constructed here; this builds the same CollectionViewSource +
/// SortDescriptions and verifies the key behaviour: the view only re-sorts on Refresh (not on an
/// item's PropertyChanged), which is exactly why the view-model refreshes when StatusSortOrder changes.
/// </summary>
public class DeviceListSortingTests
{
    private static ICollectionView BuildSortedView(ObservableCollection<Device> devices)
    {
        var view = CollectionViewSource.GetDefaultView(devices);
        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(nameof(Device.StatusSortOrder), ListSortDirection.Ascending));
        view.SortDescriptions.Add(new SortDescription(nameof(Device.TypeSortOrder), ListSortDirection.Ascending));
        view.SortDescriptions.Add(new SortDescription(nameof(Device.DeviceName), ListSortDirection.Ascending));
        return view;
    }

    private static List<string> OrderOf(ICollectionView view) => view.Cast<Device>().Select(d => d.DeviceId).ToList();

    private static Device Connected(string id, DeviceTypes type = DeviceTypes.Unknown) =>
        new()
        {
            DeviceId = id,
            DeviceName = id,
            DeviceType = type,
            SessionStartedAt = DateTime.Now,
        };

    [Fact]
    public void View_OrdersConnected_ThenHidden_ThenDisconnected()
    {
        var devices = new ObservableCollection<Device>
        {
            new() { DeviceId = "C_DISCONNECTED" }, // no session => disconnected (2)
            new()
            {
                DeviceId = "A_HIDDEN",
                SessionStartedAt = DateTime.Now,
                IsHiddenFromDisplay = true,
            }, // connected + hidden (1)
            Connected("B_CONNECTED"), // connected (0)
        };

        var view = BuildSortedView(devices);

        OrderOf(view).ShouldBe(["B_CONNECTED", "A_HIDDEN", "C_DISCONNECTED"]);
    }

    [Fact]
    public void View_WithinStatus_OrdersKeyboardsBeforeMice_ThenByName()
    {
        var devices = new ObservableCollection<Device>
        {
            Connected("zMouse", DeviceTypes.Mouse),
            Connected("aMouse", DeviceTypes.Mouse),
            Connected("Keeb", DeviceTypes.Keyboard),
            Connected("Gadget", DeviceTypes.Unknown),
        };

        var view = BuildSortedView(devices);

        // All connected: keyboards first, then mice (by name), then anything else.
        OrderOf(view).ShouldBe(["Keeb", "aMouse", "zMouse", "Gadget"]);
    }

    [Fact]
    public void HidingConnectedDevice_ReordersOnRefresh_NotOnPropertyChangeAlone()
    {
        var d1 = Connected("D1");
        var d2 = Connected("D2");
        var view = BuildSortedView(new ObservableCollection<Device> { d1, d2 });

        OrderOf(view).ShouldBe(["D1", "D2"]); // tie on status (both 0) and type => by name

        d1.IsHiddenFromDisplay = true; // D1 becomes Hidden (1); should fall behind D2 (0)

        // The CollectionView does NOT live-resort on the property change — this is the bug the fix addresses.
        OrderOf(view).ShouldBe(["D1", "D2"]);

        view.Refresh(); // what DeviceListViewModel now does on a StatusSortOrder change

        OrderOf(view).ShouldBe(["D2", "D1"]); // D1 reordered into the Hidden group
    }
}
