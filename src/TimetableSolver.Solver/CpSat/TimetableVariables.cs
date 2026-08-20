using Google.OrTools.Sat;
using TimetableSolver.Domain.Entities;
using TimetableSolver.Domain.Enums;

namespace TimetableSolver.Solver.CpSat;

public sealed class TimetableVariables
{
    private readonly Dictionary<(string SectionId, string ItemId, SchoolDay Day, int Period), BoolVar> _lessonVars = new();

    private readonly Dictionary<(string SectionId, SchoolDay Day, int Period), List<BoolVar>> _bySectionSlot = new();
    private readonly Dictionary<(string TeacherId, SchoolDay Day, int Period), List<BoolVar>> _byTeacherSlot = new();
    private readonly Dictionary<(string SectionId, string ItemId, SchoolDay Day), List<BoolVar>> _bySectionItemDay = new();
    private readonly Dictionary<(string SectionId, string ItemId), List<BoolVar>> _bySectionItem = new();

    public IReadOnlyDictionary<(string SectionId, string ItemId, SchoolDay Day, int Period), BoolVar> All => _lessonVars;

    public BoolVar Create(CpModel model, Section section, CurriculumItem item, TeachingSlot slot)
    {
        var name = $"x[{section.Id}|{item.Name}|{slot.Day}|P{slot.Period}]";
        var v = model.NewBoolVar(name);

        var key = (section.Id, item.Id, slot.Day, slot.Period);
        _lessonVars[key] = v;

        AddTo(_bySectionSlot, (section.Id, slot.Day, slot.Period), v);
        AddTo(_bySectionItemDay, (section.Id, item.Id, slot.Day), v);
        AddTo(_bySectionItem, (section.Id, item.Id), v);
        if (item.TeacherId is not null)
        {
            AddTo(_byTeacherSlot, (item.TeacherId, slot.Day, slot.Period), v);
        }

        return v;
    }

    public BoolVar? TryGet(string sectionId, string itemId, SchoolDay day, int period) =>
        _lessonVars.TryGetValue((sectionId, itemId, day, period), out var v) ? v : null;

    public IReadOnlyList<BoolVar> ForSectionSlot(string sectionId, SchoolDay day, int period) =>
        _bySectionSlot.TryGetValue((sectionId, day, period), out var list) ? list : Array.Empty<BoolVar>();

    public IReadOnlyList<BoolVar> ForTeacherSlot(string teacherId, SchoolDay day, int period) =>
        _byTeacherSlot.TryGetValue((teacherId, day, period), out var list) ? list : Array.Empty<BoolVar>();

    public IReadOnlyList<BoolVar> ForSectionItemDay(string sectionId, string itemId, SchoolDay day) =>
        _bySectionItemDay.TryGetValue((sectionId, itemId, day), out var list) ? list : Array.Empty<BoolVar>();

    public IReadOnlyList<BoolVar> ForSectionItem(string sectionId, string itemId) =>
        _bySectionItem.TryGetValue((sectionId, itemId), out var list) ? list : Array.Empty<BoolVar>();

    public IEnumerable<string> TeacherIdsWithVariables => _byTeacherSlot.Keys.Select(k => k.TeacherId).Distinct();

    private static void AddTo<TKey>(Dictionary<TKey, List<BoolVar>> dict, TKey key, BoolVar value) where TKey : notnull
    {
        if (!dict.TryGetValue(key, out var list))
        {
            list = new List<BoolVar>();
            dict[key] = list;
        }

        list.Add(value);
    }
}
