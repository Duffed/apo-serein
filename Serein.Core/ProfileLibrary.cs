namespace Serein.Core;

public static class ProfileLibrary
{
    public static bool IsNeutral(Profile p) => p.Preamp == 0 && !p.AutoHeadroom && p.Filters.Count == 0 && p.Graphic.All(x => x.Gain == 0) && p.Adjustment.All(x => x.Gain == 0);
    public static void Remove(AppState state, Guid id)
    {
        var profile = state.Profiles.Single(p => p.Id == id);
        state.Profiles.Remove(profile);
        state.TrashedProfiles.Add(profile);
        if (state.TrashedProfiles.Count > 20) state.TrashedProfiles.RemoveAt(0);
        if (profile.SourceFile != null && !state.Profiles.Any(p => string.Equals(p.SourceFile, profile.SourceFile, StringComparison.OrdinalIgnoreCase)) &&
            !state.SuppressedPeaceFiles.Contains(profile.SourceFile, StringComparer.OrdinalIgnoreCase)) state.SuppressedPeaceFiles.Add(profile.SourceFile);
        bool needsNeutral = state.Profiles.Count == 0 || state.Selected == id || state.Devices.Values.Contains(id);
        if (needsNeutral)
        {
            var neutral = state.Profiles.FirstOrDefault(IsNeutral) ?? state.Profiles.FirstOrDefault();
            if (neutral == null) { neutral = new Profile { Name = "Neutral", Favorite = true }; state.Profiles.Add(neutral); }
            if (state.Selected == id || !state.Profiles.Any(p => p.Id == state.Selected)) state.Selected = neutral.Id;
            foreach (var device in state.Devices.Where(x => x.Value == id).Select(x => x.Key).ToList()) state.Devices[device] = neutral.Id;
        }
    }
    public static Profile? RestoreLast(AppState state)
    {
        if (state.TrashedProfiles.Count == 0) return null;
        var profile = state.TrashedProfiles[^1]; state.TrashedProfiles.RemoveAt(state.TrashedProfiles.Count - 1);
        if (!state.Profiles.Any(p => p.Id == profile.Id)) state.Profiles.Add(profile);
        if (profile.SourceFile != null) state.SuppressedPeaceFiles.RemoveAll(p => string.Equals(p, profile.SourceFile, StringComparison.OrdinalIgnoreCase));
        return profile;
    }
}
