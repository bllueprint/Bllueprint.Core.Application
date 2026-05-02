using Bllueprint.Core.Domain;
using FluentAssertions;

namespace Bllueprint.Core.Application.Tests;

public class PipelineTests
{
    [Fact]
    public void CommandResult_Success_SetsEntity()
    {
        var result = CommandResult<string>.Success("hello");

        result.Entity.Should().Be("hello");
        result.NotFound.Should().BeFalse();
        result.HasErrors.Should().BeFalse();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void CommandResult_Missing_SetsNotFound()
    {
        var result = CommandResult<string>.Missing();

        result.NotFound.Should().BeTrue();
        result.Entity.Should().BeNull();
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void CommandResult_Failed_SetsErrors()
    {
        Notification[] notifications =
        [
            new Notification { Message = "err1", TransitionName = "Step1", Kind = NotificationKind.Error },
            new Notification { Message = "err2", TransitionName = "Step2", Kind = NotificationKind.Error }
        ];

        var result = CommandResult<string>.Failed(notifications);

        result.HasErrors.Should().BeTrue();
        result.Errors.Should().HaveCount(2);
        result.Errors.Select(e => e.Message).Should().BeEquivalentTo("err1", "err2");
        result.Entity.Should().BeNull();
        result.NotFound.Should().BeFalse();
    }

    [Fact]
    public void CollectionResult_Success_SetsItems()
    {
        int[] items = [1, 2, 3];

        var result = CollectionResult<int>.Success(items);

        result.Entity.Should().BeEquivalentTo(items);
        result.NotFound.Should().BeFalse();
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void CollectionResult_Missing_SetsNotFound()
    {
        var result = CollectionResult<int>.Missing();

        result.NotFound.Should().BeTrue();
        result.Entity.Should().BeNull();
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void CollectionResult_Failed_SetsErrors()
    {
        Notification[] notifications =
        [
            new Notification { Message = "bad", TransitionName = "Step", Kind = NotificationKind.Error }
        ];

        var result = CollectionResult<int>.Failed(notifications);

        result.HasErrors.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message == "bad");
        result.Entity.Should().BeNull();
    }

    [Fact]
    public async Task HandlerPipeline_SingleInvoke_ReturnsSuccess()
    {
        (HandlerPipeline<string>? pipeline, PipelineContext _) = PipelineFixture.StartPipeline(() => Task.FromResult<string?>("entity"));

        ICommandResult<string> result = await pipeline.ToResultAsync();

        result.HasErrors.Should().BeFalse();
        result.NotFound.Should().BeFalse();
        result.Entity.Should().Be("entity");
    }

    [Fact]
    public async Task HandlerPipeline_ChainedInvokes_PropagatesEntity()
    {
        (HandlerPipeline<string>? pipeline, PipelineContext _) = PipelineFixture.StartPipeline(() => Task.FromResult<string?>("first"));

        ICommandResult<int?> result = await pipeline
            .Invoke(s => Task.FromResult<int?>(s.Length))
            .ToResultAsync();

        result.Entity.Should().Be(5); // "first".Length
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public async Task HandlerPipeline_ActionInvoke_MutatesAndContinues()
    {
        (HandlerPipeline<List<int>>? pipeline, PipelineContext _) = PipelineFixture.StartPipeline(() => Task.FromResult<List<int>?>(new List<int> { 1 }));
        bool mutated = false;

        ICommandResult<List<int>> result = await pipeline
            .Invoke(list =>
            {
                list.Add(2);
                mutated = true;
            }).ToResultAsync();

        mutated.Should().BeTrue();
        result.Entity.Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public async Task HandlerPipeline_Save_PersistsAndReturnsEntity()
    {
        (HandlerPipeline<string>? pipeline, PipelineContext _) = PipelineFixture.StartPipeline(() => Task.FromResult<string?>("data"));
        bool saved = false;

        ICommandResult<string> result = await pipeline
            .Save(_ =>
            {
                saved = true;
                return Task.CompletedTask;
            }).ToResultAsync();

        saved.Should().BeTrue();
        result.Entity.Should().Be("data");
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public async Task HandlerPipeline_NullEntity_ReturnsMissing()
    {
        (HandlerPipeline<string>? pipeline, PipelineContext _) = PipelineFixture.StartPipeline(() => Task.FromResult<string?>(null));

        ICommandResult<string> result = await pipeline.ToResultAsync();

        result.NotFound.Should().BeTrue();
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public async Task HandlerPipeline_InvokeThrows_RecordsErrorAndFails()
    {
        (HandlerPipeline<string>? pipeline, PipelineContext _) = PipelineFixture.StartPipeline(() => Task.FromResult<string?>("entity"));

        ICommandResult<int?> result = await pipeline
            .Invoke(_ => Task.FromException<int?>(new InvalidOperationException("boom")))
            .ToResultAsync();

        result.HasErrors.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message == "boom");
    }

    [Fact]
    public async Task HandlerPipeline_SaveThrows_RecordsError()
    {
        (HandlerPipeline<string>? pipeline, PipelineContext _) = PipelineFixture.StartPipeline(() => Task.FromResult<string?>("entity"));

        ICommandResult<string> result = await pipeline
            .Save(_ => Task.FromException(new Exception("save failed")))
            .ToResultAsync();

        result.HasErrors.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message == "save failed");
    }

    [Fact]
    public async Task HandlerPipeline_GuardTaskThrows_RecordsError()
    {
        (HandlerPipeline<string>? pipeline, PipelineContext _) = PipelineFixture.StartPipeline(() => Task.FromResult<string?>("entity"));

        ICommandResult<string> result = await pipeline
            .Invoke(() => Task.FromException(new Exception("guard failed")))
            .ToResultAsync();

        result.HasErrors.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message == "guard failed");
    }

    [Fact]
    public async Task HandlerPipeline_SkipsStepsAfterFailure()
    {
        (HandlerPipeline<string>? pipeline, PipelineContext _) = PipelineFixture.StartPipeline(() => Task.FromResult<string?>("entity"));
        bool laterStepRan = false;

        ICommandResult<int?> result = await pipeline
            .Invoke(_ => Task.FromException<int?>(new Exception("first fail")))
            .Invoke(_ =>
            {
                laterStepRan = true;
                return Task.FromResult<int?>(99);
            }).ToResultAsync();

        laterStepRan.Should().BeFalse();
        result.HasErrors.Should().BeTrue();
    }

    [Fact]
    public async Task WithCheck_PredicateTrue_ContinuesPipeline()
    {
        (HandlerPipeline<string>? pipeline, PipelineContext _) = PipelineFixture.StartPipeline(() => Task.FromResult<string?>("entity"));

        ICommandResult<int?> result = await pipeline
            .WithCheck(s => s.Length > 0)
            .Invoke(s => Task.FromResult<int?>(s.Length))
            .ToResultAsync();

        result.Entity.Should().Be(6);
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public async Task WithCheck_PredicateFalse_FailsWithDefaultMessage()
    {
        (HandlerPipeline<string>? pipeline, PipelineContext _) = PipelineFixture.StartPipeline(() => Task.FromResult<string?>("entity"));

        ICommandResult<string> result = await pipeline
            .WithCheck(s => s.Length == 0)
            .Invoke(_ => Task.FromResult<string?>(null))
            .ToResultAsync();

        result.HasErrors.Should().BeTrue();
        result.Errors.Should().ContainSingle();
    }

    [Fact]
    public async Task WithCheck_WithMessage_UsesCustomMessage()
    {
        (HandlerPipeline<string>? pipeline, PipelineContext _) = PipelineFixture.StartPipeline(() => Task.FromResult<string?>("entity"));

        ICommandResult<string> result = await pipeline
            .WithCheck(s => s.Length == 0)
            .WithMessage("Custom validation message")
            .Invoke(_ => Task.FromResult<string?>(null))
            .ToResultAsync();

        result.Errors.Should().ContainSingle(e => e.Message == "Custom validation message");
    }

    [Fact]
    public async Task WithCheck_ActionInvoke_PredicateFalse_Fails()
    {
        (HandlerPipeline<string>? pipeline, PipelineContext _) = PipelineFixture.StartPipeline(() => Task.FromResult<string?>("entity"));
        bool actionRan = false;

        ICommandResult<string> result = await pipeline
            .WithCheck(s => false)
            .WithMessage("blocked")
            .Invoke(s => { actionRan = true; })
            .ToResultAsync();

        actionRan.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Message == "blocked");
    }

    [Fact]
    public async Task WithCheck_SaveInvoke_PredicateFalse_DoesNotSave()
    {
        (HandlerPipeline<string>? pipeline, PipelineContext _) = PipelineFixture.StartPipeline(() => Task.FromResult<string?>("entity"));
        int savedCount = 0;

        ICommandResult<string> result = await pipeline
            .WithCheck(s => false)
            .Save(_ =>
            {
                savedCount++;
                return Task.CompletedTask;
            }).ToResultAsync();

        savedCount.Should().Be(0);
        result.HasErrors.Should().BeTrue();
    }

    [Fact]
    public async Task WithCheck_GuardTaskThrows_FailsWithGenericMessage()
    {
        (HandlerPipeline<string>? pipeline, PipelineContext _) = PipelineFixture.StartPipeline(() => Task.FromResult<string?>("entity"));

        ICommandResult<string> result = await pipeline
            .WithCheck(s => true)
            .Invoke(() => Task.FromException(new Exception("guard boom")))
            .ToResultAsync();

        result.HasErrors.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message == "An unexpected error occurred.");
    }

    [Fact]
    public async Task GuardPipeline_PredicateSatisfied_ContinuesToEntity()
    {
        PipelineContext ctx = PipelineFixture.NewContext();
        var steps = new List<PipelineStep>();
        var guardPipeline = new GuardPipeline(() => Task.FromResult(true), steps, ctx);

        ICommandResult<string> result = await guardPipeline
            .With(r => r)
            .Invoke(() => Task.FromResult<string?>("entity"))
            .ToResultAsync();

        result.Entity.Should().Be("entity");
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public async Task GuardPipeline_PredicateNotSatisfied_FailsWithMessage()
    {
        PipelineContext ctx = PipelineFixture.NewContext();
        var steps = new List<PipelineStep>();
        var guardPipeline = new GuardPipeline(() => Task.FromResult(false), steps, ctx);

        ICommandResult<string> result = await guardPipeline
            .With(r => r)
            .WithMessage("Guard check failed")
            .Invoke(() => Task.FromResult<string?>("entity"))
            .ToResultAsync();

        result.HasErrors.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message == "Guard check failed");
    }

    [Fact]
    public async Task GuardPipeline_GuardThrows_FailsWithMessage()
    {
        PipelineContext ctx = PipelineFixture.NewContext();
        var steps = new List<PipelineStep>();
        var guardPipeline = new GuardPipeline(
            () => Task.FromException<bool>(new Exception("guard blew up")),
            steps,
            ctx);

        ICommandResult<string> result = await guardPipeline
            .With(r => r)
            .WithMessage("Guard exception message")
            .Invoke(() => Task.FromResult<string?>("entity"))
            .ToResultAsync();

        result.HasErrors.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message == "Guard exception message");
    }

    [Fact]
    public async Task ExceptionGuardPipeline_GuardSucceeds_ContinuesToEntity()
    {
        PipelineContext ctx = PipelineFixture.NewContext();
        var steps = new List<PipelineStep>();
        var guardPipeline = new ExceptionGuardPipeline(
            () => Task.CompletedTask,
            steps,
            ctx);

        ICommandResult<string> result = await guardPipeline
            .Invoke(() => Task.FromResult<string?>("entity"))
            .ToResultAsync();

        result.Entity.Should().Be("entity");
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public async Task ExceptionGuardPipeline_GuardThrows_UsesDefaultMessage()
    {
        PipelineContext ctx = PipelineFixture.NewContext();
        var steps = new List<PipelineStep>();
        var guardPipeline = new ExceptionGuardPipeline(
            () => Task.FromException(new Exception("internal")),
            steps,
            ctx);

        ICommandResult<string> result = await guardPipeline
            .Invoke(() => Task.FromResult<string?>("entity"))
            .ToResultAsync();

        result.HasErrors.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message == "An unexpected error occurred.");
    }

    [Fact]
    public async Task ExceptionGuardPipeline_WithMessage_GuardThrows_UsesCustomMessage()
    {
        PipelineContext ctx = PipelineFixture.NewContext();
        var steps = new List<PipelineStep>();
        var guardPipeline = new ExceptionGuardPipeline(
            () => Task.FromException(new Exception("internal")),
            steps,
            ctx);

        ICommandResult<string> result = await guardPipeline
            .WithMessage("Something went wrong with the guard")
            .Invoke(() => Task.FromResult<string?>("entity"))
            .ToResultAsync();

        result.Errors.Should().ContainSingle(e => e.Message == "Something went wrong with the guard");
    }

    [Fact]
    public async Task ExceptionGuardPipeline_AlreadyFailed_SkipsGuardAndEntity()
    {
        PipelineContext ctx = PipelineFixture.NewContext();
        ctx.Fail("Pre-existing failure");

        var steps = new List<PipelineStep>();
        bool entityRan = false;

        var guardPipeline = new ExceptionGuardPipeline(
            () => Task.CompletedTask,
            steps,
            ctx);

        ICommandResult<string> result = await guardPipeline
            .Invoke(() =>
            {
                entityRan = true;
                return Task.FromResult<string?>("entity");
            }).ToResultAsync();

        entityRan.Should().BeFalse();
        result.HasErrors.Should().BeTrue();
    }

    [Fact]
    public void PipelineContext_InitialState_NotFailed()
    {
        PipelineContext ctx = PipelineFixture.NewContext();

        ctx.Failed.Should().BeFalse();
        ctx.Notifications.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void PipelineContext_FailWithString_MarksFailed()
    {
        PipelineContext ctx = PipelineFixture.NewContext();

        ctx.Fail("Something broke");

        ctx.Failed.Should().BeTrue();
        ctx.Notifications.ValidationErrors
            .Should().ContainSingle(e => e.Message == "Something broke");
    }

    [Fact]
    public void PipelineContext_FailWithDetail_MarksFailed()
    {
        PipelineContext ctx = PipelineFixture.NewContext();

        ctx.Fail(new FailureDetail
        {
            Message = "Detailed error",
            TransitionName = "MyStep",
            Kind = NotificationKind.Error
        });

        ctx.Failed.Should().BeTrue();
        ctx.Notifications.ValidationErrors
            .Should().ContainSingle(e => e.Message == "Detailed error" && e.TransitionName == "MyStep");
    }

    [Fact]
    public void PipelineContext_FailWithDetailNullKind_DefaultsToError()
    {
        PipelineContext ctx = PipelineFixture.NewContext();

        ctx.Fail(new FailureDetail
        {
            Message = "No kind set",
            TransitionName = "Step",
            Kind = null
        });

        ctx.Notifications.ValidationErrors
            .Should().ContainSingle(e => e.Kind == NotificationKind.Error);
    }

    [Fact]
    public void PipelineGuard_NotNull_ReturnsTrueForNonNull()
    {
        Func<string, bool> guard = PipelineGuard.NotNull<string>();

        guard("hello").Should().BeTrue();
    }

    [Fact]
    public void PipelineGuard_NotNull_ReturnsFalseForNull()
    {
        Func<string, bool> guard = PipelineGuard.NotNull<string>();

        guard(null!).Should().BeFalse();
    }

    [Fact]
    public void PipelineGuard_NotEmpty_ReturnsTrueForNonDefault()
    {
        Func<SampleEntity, bool> guard = PipelineGuard.NotEmpty<SampleEntity, Guid>(e => e.Id);

        guard(new SampleEntity { Id = Guid.NewGuid() }).Should().BeTrue();
    }

    [Fact]
    public void PipelineGuard_NotEmpty_ReturnsFalseForDefault()
    {
        Func<SampleEntity, bool> guard = PipelineGuard.NotEmpty<SampleEntity, Guid>(e => e.Id);

        guard(new SampleEntity { Id = Guid.Empty }).Should().BeFalse();
    }

    [Fact]
    public void ICommandResult_HasErrors_TrueWhenErrorsPresent()
    {
        ICommandResult<string> result = CommandResult<string>.Failed(
            [new Notification { Message = "x", TransitionName = "t", Kind = NotificationKind.Error }]);

        result.HasErrors.Should().BeTrue();
    }

    [Fact]
    public void ICommandResult_HasErrors_FalseWhenNoErrors()
    {
        ICommandResult<string> result = CommandResult<string>.Success("ok");

        result.HasErrors.Should().BeFalse();
    }
}
