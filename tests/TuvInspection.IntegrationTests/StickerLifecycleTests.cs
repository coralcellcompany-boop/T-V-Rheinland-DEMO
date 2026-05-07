using FluentAssertions;
using TuvInspection.Domain.Stickers;

namespace TuvInspection.IntegrationTests;

/// <summary>
/// Domain-level tests for the Sticker aggregate. Verifies the state machine and the
/// guards that protect each transition — this is what the rest of the application
/// relies on, so we test it directly instead of via the API.
/// </summary>
public class StickerLifecycleTests
{
    private static readonly DateTime NowUtc = new(2026, 5, 6, 10, 0, 0, DateTimeKind.Utc);
    private const string InspectorId = "inspector-1";
    private static readonly Guid CertId = Guid.NewGuid();
    private static readonly Guid EquipId = Guid.NewGuid();
    private static readonly Guid ClientId = Guid.NewGuid();

    private static Sticker NewSticker(StickerColor color = StickerColor.Blue) =>
        new(Guid.NewGuid(), $"TUVR{Random.Shared.Next(100000, 999999):D6}", color);

    [Fact]
    public void Construct_DefaultsTo_Unallocated_With_Color()
    {
        var s = new Sticker(Guid.NewGuid(), "TUVR000001", StickerColor.Green);
        s.State.Should().Be(StickerState.Unallocated);
        s.Color.Should().Be(StickerColor.Green);
        s.StickerNo.Should().Be("TUVR000001");
    }

    [Fact]
    public void Construct_NormalizesStickerNumber_ToUpperInvariant()
    {
        var s = new Sticker(Guid.NewGuid(), "  tuvr000042 ");
        s.StickerNo.Should().Be("TUVR000042");
    }

    [Fact]
    public void Construct_RejectsBlankStickerNumber()
    {
        var act = () => new Sticker(Guid.NewGuid(), "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AssignToInspector_FromUnallocated_SetsInspectorAndTime()
    {
        var s = NewSticker();
        s.AssignToInspector(InspectorId, fromRequestId: null, NowUtc);
        s.AssignedToInspectorId.Should().Be(InspectorId);
        s.AssignedAtUtc.Should().Be(NowUtc);
        s.State.Should().Be(StickerState.Unallocated);
    }

    [Fact]
    public void AssignToInspector_FromIssued_Throws()
    {
        var s = NewSticker();
        s.Issue(CertId, EquipId, ClientId, DateOnly.FromDateTime(NowUtc).AddYears(1), NowUtc);
        var act = () => s.AssignToInspector(InspectorId, null, NowUtc);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Unallocated*");
    }

    [Fact]
    public void Issue_FromUnallocated_SetsLinksAndExpiry()
    {
        var s = NewSticker();
        var due = DateOnly.FromDateTime(NowUtc).AddYears(1);

        s.Issue(CertId, EquipId, ClientId, due, NowUtc);

        s.State.Should().Be(StickerState.Issued);
        s.IssuedToCertificateId.Should().Be(CertId);
        s.IssuedToEquipmentId.Should().Be(EquipId);
        s.ClientId.Should().Be(ClientId);
        s.ValidUntil.Should().Be(due);
        s.IssuedAtUtc.Should().Be(NowUtc);
    }

    [Fact]
    public void Issue_AfterIssue_Throws()
    {
        var s = NewSticker();
        s.Issue(CertId, EquipId, ClientId, null, NowUtc);
        var act = () => s.Issue(Guid.NewGuid(), EquipId, ClientId, null, NowUtc);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Void_FromIssued_TerminatesWithReason()
    {
        var s = NewSticker();
        s.Issue(CertId, EquipId, ClientId, null, NowUtc);

        s.Void("Damaged in transit", NowUtc);

        s.State.Should().Be(StickerState.Voided);
        s.VoidReason.Should().Be("Damaged in transit");
        s.VoidedAtUtc.Should().Be(NowUtc);
    }

    [Fact]
    public void Void_FromTerminalState_Throws()
    {
        var s = NewSticker();
        s.Void("Initial void", NowUtc);
        var act = () => s.Void("Second void", NowUtc);
        act.Should().Throw<InvalidOperationException>().WithMessage("*terminal*");
    }

    [Fact]
    public void MarkReplacedBy_FromIssued_SetsReplacementAndState()
    {
        var s = NewSticker();
        s.Issue(CertId, EquipId, ClientId, null, NowUtc);
        var replacementId = Guid.NewGuid();

        s.MarkReplacedBy(replacementId);

        s.State.Should().Be(StickerState.Replaced);
        s.ReplacedByStickerId.Should().Be(replacementId);
    }

    [Fact]
    public void MarkReplacedBy_FromUnallocated_Throws()
    {
        var s = NewSticker();
        var act = () => s.MarkReplacedBy(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Expire_OnIssued_TransitionsToExpired()
    {
        var s = NewSticker();
        s.Issue(CertId, EquipId, ClientId, null, NowUtc);
        s.Expire();
        s.State.Should().Be(StickerState.Expired);
    }

    [Fact]
    public void Expire_OnUnallocated_NoOp()
    {
        var s = NewSticker();
        s.Expire();
        s.State.Should().Be(StickerState.Unallocated);
    }

    [Fact]
    public void Unassign_ClearsInspector_DoesNotChangeState()
    {
        var s = NewSticker();
        s.AssignToInspector(InspectorId, null, NowUtc);
        s.Unassign();
        s.AssignedToInspectorId.Should().BeNull();
        s.AssignedAtUtc.Should().BeNull();
        s.State.Should().Be(StickerState.Unallocated);
    }
}
