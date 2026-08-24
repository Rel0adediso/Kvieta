using KardesKilidi.Core.Models;

namespace KardesKilidi.App.ViewModels;

public sealed class TemporaryAllowanceRow(TemporaryAllowance model)
{
    public Guid Id { get; } = model.Id;
    public DateOnly Date { get; } = model.Date;
    public TimeOnly AllowedFrom { get; } = model.AllowedFrom;
    public TimeOnly AllowedUntil { get; } = model.AllowedUntil;
    public int BonusMinutes { get; } = model.BonusMinutes;
    public string Note { get; } = model.Note;
    public string DateText => Date.ToString("dd.MM.yyyy");
    public string TimeText => $"{AllowedFrom:HH\\:mm}–{AllowedUntil:HH\\:mm}";
    public string BonusText => $"+{BonusMinutes} {Services.LocalizationService.Get("MinuteShort")}";

    public TemporaryAllowance ToModel() => new()
    {
        Id = Id,
        Date = Date,
        AllowedFrom = AllowedFrom,
        AllowedUntil = AllowedUntil,
        BonusMinutes = BonusMinutes,
        Note = Note
    };
}
