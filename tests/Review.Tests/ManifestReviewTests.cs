using Blocks.Domain;
using Review.Application.Features.ManifestReviews.RiskScanning;
using Review.Domain.ManifestReviews;

namespace Review.Tests;

public class ManifestReviewTests
{
    private const string Hash = "1111111111111111111111111111111111111111111111111111111111111111";
    private const string NewerHash = "2222222222222222222222222222222222222222222222222222222222222222";

    [Fact]
    public void Two_different_reviewers_approve_and_the_decision_names_both()
    {
        var review = Open(TestData.CrmTools);

        review.Approve(null, TestData.ReviewMachines, Approve("alice"));
        Assert.Equal((ReviewStatus.PartiallyApproved, "alice"), (review.Status, review.FirstApproverId));
        Assert.Empty(review.DomainEvents);

        review.Approve(Hash, TestData.ReviewMachines, Approve("bob"));

        Assert.Equal(ReviewStatus.Approved, review.Status);
        var completed = Assert.IsType<ManifestReviewCompleted>(Assert.Single(review.DomainEvents));
        Assert.Equal(["alice", "bob"], completed.DecidedBy);
    }

    [Fact]
    public void One_person_cannot_give_both_approvals()
    {
        var review = Open(TestData.CrmTools);
        review.Approve(null, TestData.ReviewMachines, Approve("alice"));

        var exception = Assert.Throws<DomainException>(() => review.Approve(Hash, TestData.ReviewMachines, Approve("alice")));

        Assert.Contains("Four-eyes", exception.Message);
        Assert.Equal(ReviewStatus.PartiallyApproved, review.Status);
        Assert.Empty(review.DomainEvents);
    }

    [Theory]
    [InlineData(NewerHash)] // the server drifted again after the scan
    [InlineData("")]        // nothing pending any more
    [InlineData(null)]      // Registry could not be asked
    public void The_final_approval_is_refused_when_the_reviewed_manifest_is_no_longer_pending(string? currentPendingHash)
    {
        var review = Open(TestData.CrmTools);
        review.Approve(null, TestData.ReviewMachines, Approve("alice"));

        Assert.Throws<DomainException>(() => review.Approve(currentPendingHash, TestData.ReviewMachines, Approve("bob")));
        Assert.Equal(ReviewStatus.PartiallyApproved, review.Status);
    }

    [Fact]
    public void A_high_risk_manifest_needs_a_written_reason_from_each_approver()
    {
        var review = Open(TestData.PoisonedCrmTools, TestData.CrmTools);
        Assert.Equal(RiskLevel.High, review.RiskLevel);

        Assert.Throws<DomainException>(() => review.Approve(null, TestData.ReviewMachines, Approve("alice")));
        review.Approve(null, TestData.ReviewMachines, Approve("alice", "False positive: vendor confirmed the tag is a docs artefact."));

        Assert.Equal(ReviewStatus.PartiallyApproved, review.Status);
    }

    [Fact]
    public void One_reviewer_can_reject_but_must_say_why()
    {
        var review = Open(TestData.PoisonedCrmTools, TestData.CrmTools);

        Assert.Throws<DomainException>(() => review.Reject(TestData.ReviewMachines, new TestAction<ReviewActionType>(ReviewActionType.Reject, "alice")));
        review.Reject(TestData.ReviewMachines, new TestAction<ReviewActionType>(ReviewActionType.Reject, "alice", "Hidden instructions."));

        Assert.Equal(ReviewStatus.Rejected, review.Status);
        Assert.Equal(["alice"], Assert.IsType<ManifestReviewCompleted>(Assert.Single(review.DomainEvents)).DecidedBy);
    }

    [Fact]
    public void Decided_reviews_cannot_be_decided_again_or_superseded()
    {
        var review = Open(TestData.CrmTools);
        review.Approve(null, TestData.ReviewMachines, Approve("alice"));
        review.Approve(Hash, TestData.ReviewMachines, Approve("bob"));

        Assert.Throws<DomainException>(() => review.Approve(Hash, TestData.ReviewMachines, Approve("carol")));
        Assert.Throws<DomainException>(() => review.Reject(TestData.ReviewMachines, new TestAction<ReviewActionType>(ReviewActionType.Reject, "carol", "No.")));
        Assert.Throws<DomainException>(() => review.Supersede(TestData.ReviewMachines, new TestAction<ReviewActionType>(ReviewActionType.Supersede, "system")));
    }

    [Fact]
    public void A_superseded_review_cannot_be_approved()
    {
        var review = Open(TestData.CrmTools);
        review.Supersede(TestData.ReviewMachines, new TestAction<ReviewActionType>(ReviewActionType.Supersede, "system"));

        Assert.Throws<DomainException>(() => review.Approve(null, TestData.ReviewMachines, Approve("alice")));
        Assert.Equal(ReviewStatus.Superseded, review.Status);
    }

    [Fact]
    public void Opening_a_review_announces_the_scan_verdict()
    {
        var review = ManifestReview.Open(Guid.CreateVersion7(), "crm", Hash, TestData.PoisonedCrmTools, NewerHash, TestData.CrmTools, "drift",
            HeuristicRiskScanner.Scan(new("crm", TestData.PoisonedCrmTools, TestData.CrmTools)),
            new TestAction<ReviewActionType>(ReviewActionType.Open, "system:review"));

        Assert.Equal(RiskLevel.High, Assert.IsType<ManifestReviewOpened>(Assert.Single(review.DomainEvents)).Review.RiskLevel);
    }

    private static ManifestReview Open(string toolsJson, string? previousToolsJson = null)
    {
        var review = ManifestReview.Open(Guid.CreateVersion7(), "crm", Hash, toolsJson, previousToolsJson is null ? null : NewerHash,
            previousToolsJson, "test", HeuristicRiskScanner.Scan(new("crm", toolsJson, previousToolsJson)),
            new TestAction<ReviewActionType>(ReviewActionType.Open, "system:review"));
        review.ClearDomainEvents();
        return review;
    }

    private static TestAction<ReviewActionType> Approve(string actorId, string? comment = null) => new(ReviewActionType.Approve, actorId, comment);
}
