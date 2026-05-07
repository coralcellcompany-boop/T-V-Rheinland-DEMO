using FluentAssertions;
using TuvInspection.Domain.Stickers;

namespace TuvInspection.IntegrationTests;

/// <summary>
/// Domain-level tests for the StickerRequest aggregate. The approve→assign workflow
/// goes through this aggregate before any sticker is reserved, so its state machine
/// is the gate that protects the rest of the flow.
/// </summary>
public class StickerRequestLifecycleTests
{
    private static readonly DateTime NowUtc = new(2026, 5, 6, 10, 0, 0, DateTimeKind.Utc);
    private const string InspectorId = "inspector-1";
    private const string ApproverId = "coordinator-1";

    private static StickerRequest NewPendingRequest(int qty = 25, StickerColor color = StickerColor.Blue) =>
        new(Guid.NewGuid(), "SR-2026-0001", InspectorId, color, qty,
            "JOD2026-0042 — Yanbu refinery, 3-day visit.");

    [Fact]
    public void Construct_StartsPending_AndNormalizesRequestNo()
    {
        var r = new StickerRequest(Guid.NewGuid(), "  sr-2026-0007 ", InspectorId,
            StickerColor.Blue, 10, "test");
        r.RequestNo.Should().Be("SR-2026-0007");
        r.State.Should().Be(StickerRequestState.Pending);
        r.Quantity.Should().Be(10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(501)]
    public void Construct_RejectsOutOfRangeQuantity(int qty)
    {
        var act = () => new StickerRequest(Guid.NewGuid(), "SR-2026-0001", InspectorId,
            StickerColor.Blue, qty, null);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Approve_FromPending_AdvancesAndRecordsAllocation()
    {
        var r = NewPendingRequest();
        r.Approve(ApproverId, "Looks good.", allocated: 25, NowUtc);

        r.State.Should().Be(StickerRequestState.Approved);
        r.DecidedByUserId.Should().Be(ApproverId);
        r.DecidedAtUtc.Should().Be(NowUtc);
        r.DecisionComments.Should().Be("Looks good.");
        r.AllocatedCount.Should().Be(25);
    }

    [Fact]
    public void Approve_RecordsPartialAllocation_WhenStockShort()
    {
        var r = NewPendingRequest(qty: 50);
        r.Approve(ApproverId, "Partial — pool ran out.", allocated: 18, NowUtc);

        r.State.Should().Be(StickerRequestState.Approved);
        r.AllocatedCount.Should().Be(18);
        r.Quantity.Should().Be(50);
    }

    [Fact]
    public void Approve_FromNonPending_Throws()
    {
        var r = NewPendingRequest();
        r.Approve(ApproverId, null, allocated: 25, NowUtc);
        var act = () => r.Approve(ApproverId, null, allocated: 25, NowUtc);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reject_FromPending_AdvancesWithReason()
    {
        var r = NewPendingRequest();
        r.Reject(ApproverId, "Quantity is excessive.", NowUtc);

        r.State.Should().Be(StickerRequestState.Rejected);
        r.DecisionComments.Should().Be("Quantity is excessive.");
        r.DecidedByUserId.Should().Be(ApproverId);
    }

    [Fact]
    public void Reject_AfterApprove_Throws()
    {
        var r = NewPendingRequest();
        r.Approve(ApproverId, null, 25, NowUtc);
        var act = () => r.Reject(ApproverId, "Changed my mind.", NowUtc);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_FromPending_AdvancesToCancelled()
    {
        var r = NewPendingRequest();
        r.Cancel(NowUtc);
        r.State.Should().Be(StickerRequestState.Cancelled);
    }

    [Fact]
    public void Cancel_AfterDecision_Throws()
    {
        var r = NewPendingRequest();
        r.Approve(ApproverId, null, 25, NowUtc);
        var act = () => r.Cancel(NowUtc);
        act.Should().Throw<InvalidOperationException>();
    }
}
