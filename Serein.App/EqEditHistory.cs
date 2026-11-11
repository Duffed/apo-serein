using Serein.Core;

namespace Serein.App;

// Only EQ values belong to this history; names, sources and device mappings stay independent.
public sealed class EqEditHistory(Profile profile)
{
    readonly List<EqSettings> undo = [], redo = [];
    EqSettings baseline = EqSettings.Capture(profile);
    bool grouping;
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    static void Push(List<EqSettings> stack, EqSettings value) { stack.Add(value); if (stack.Count > 100) stack.RemoveAt(0); }
    public void Record()
    {
        if (grouping) return;
        var next = EqSettings.Capture(profile);
        if (baseline.Same(next)) return;
        Push(undo, baseline); baseline = next; redo.Clear();
    }
    public void Begin() { Record(); grouping = true; }
    public void End() { grouping = false; Record(); }
    public bool Undo()
    {
        if (grouping) return false;
        Record(); if (!CanUndo) return false;
        Push(redo, baseline); baseline = undo[^1]; undo.RemoveAt(undo.Count - 1); baseline.Restore(profile); return true;
    }
    public bool Redo()
    {
        if (grouping) return false;
        Record(); if (!CanRedo) return false;
        Push(undo, baseline); baseline = redo[^1]; redo.RemoveAt(redo.Count - 1); baseline.Restore(profile); return true;
    }
}

