namespace Serein.Core;
public static class DeviceProfiles
{
    public static Profile Resolve(AppState state, IList<Profile> profiles, string deviceId, string deviceName, Profile current, bool automatic)
    {
        if (state.Devices.TryGetValue(deviceId, out var id) && profiles.FirstOrDefault(p => p.Id == id) is Profile existing) return existing;
        var next = automatic ? new Profile { Name = "Neutral · " + deviceName } : current;
        if (automatic) profiles.Add(next);
        state.Devices[deviceId] = next.Id;
        return next;
    }
}
