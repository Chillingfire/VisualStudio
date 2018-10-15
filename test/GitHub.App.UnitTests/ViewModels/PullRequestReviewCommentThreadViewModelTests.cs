﻿using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using GitHub.Factories;
using GitHub.Models;
using GitHub.Models.Drafts;
using GitHub.Primitives;
using GitHub.Services;
using GitHub.ViewModels;
using NSubstitute;
using NUnit.Framework;
using ReactiveUI.Testing;

namespace GitHub.InlineReviews.UnitTests.ViewModels
{
    public class PullRequestReviewCommentThreadViewModelTests
    {
        [Test]
        public async Task CreatesComments()
        {
            var target = await CreateTarget(
                comments: CreateComments("Comment 1", "Comment 2"));

            Assert.That(3, Is.EqualTo(target.Comments.Count));
            Assert.That(
                target.Comments.Select(x => x.Body),
                Is.EqualTo(new[]
                {
                    "Comment 1",
                    "Comment 2",
                    null,
                }));

            Assert.That(
                new[]
                {
                    CommentEditState.None,
                    CommentEditState.None,
                    CommentEditState.Placeholder,
                },
                Is.EqualTo(target.Comments.Select(x => x.EditState)));
        }

        [Test]
        public async Task PlaceholderCommitEnabledWhenCommentHasBody()
        {
            var target = await CreateTarget(
                comments: CreateComments("Comment 1"));

            Assert.That(target.Comments[1].CommitEdit.CanExecute(null), Is.False);

            target.Comments[1].Body = "Foo";
            Assert.That(target.Comments[1].CommitEdit.CanExecute(null), Is.True);
        }

        [Test]
        public async Task PostsCommentInReplyToCorrectComment()
        {
            using (TestUtils.WithScheduler(Scheduler.CurrentThread))
            {
                var session = CreateSession();
                var target = await CreateTarget(
                    session: session,
                    comments: CreateComments("Comment 1", "Comment 2"));

                target.Comments[2].Body = "New Comment";
                await target.Comments[2].CommitEdit.Execute();

                session.Received(1).PostReviewComment("New Comment", "1");
            }
        }

        [Test]
        public async Task LoadsDraftForNewComment()
        {
            var draftStore = Substitute.For<IMessageDraftStore>();

            draftStore.GetDraft<CommentDraft>(
                "pr-review-comment|https://github.com/owner/repo|47|file.cs", "10")
                .Returns(new CommentDraft
                {
                    Body = "Draft comment.",
                });

            var target = await CreateTarget(draftStore: draftStore, newThread: true);

            Assert.That(target.Comments[0].Body, Is.EqualTo("Draft comment."));
        }

        async Task<PullRequestReviewCommentThreadViewModel> CreateTarget(
            IMessageDraftStore draftStore = null,
            IViewViewModelFactory factory = null,
            IPullRequestSession session = null,
            IPullRequestSessionFile file = null,
            PullRequestReviewModel review = null,
            IEnumerable<InlineCommentModel> comments = null,
            bool newThread = false)
        {
            draftStore = draftStore ?? Substitute.For<IMessageDraftStore>();
            factory = factory ?? CreateFactory();
            session = session ?? CreateSession();
            file = file ?? CreateFile();
            review = review ?? new PullRequestReviewModel();
            comments = comments ?? CreateComments();

            var thread = Substitute.For<IInlineCommentThreadModel>();
            thread.Comments.Returns(comments.ToList());

            var result = new PullRequestReviewCommentThreadViewModel(draftStore, factory);

            if (newThread)
            {
                await result.InitializeNewAsync(session, file, 10, DiffSide.Left, true);
            }
            else
            {
                await result.InitializeAsync(session, file, review, thread, true);
            }

            return result;
        }

        InlineCommentModel CreateComment(string id, string body)
        {
            return new InlineCommentModel
            {
                Comment = new PullRequestReviewCommentModel
                {
                    Id = id,
                    Body = body,
                },
                Review = new PullRequestReviewModel(),
            };
        }

        IEnumerable<InlineCommentModel> CreateComments(params string[] bodies)
        {
            var id = 1;

            foreach (var body in bodies)
            {
                yield return CreateComment((id++).ToString(), body);
            }
        }

        IPullRequestSessionFile CreateFile(string relativePath = "file.cs")
        {
            var result = Substitute.For<IPullRequestSessionFile>();
            result.RelativePath.Returns(relativePath);
            return result;
        }

        IViewViewModelFactory CreateFactory()
        {
            var result = Substitute.For<IViewViewModelFactory>();
            var commentService = Substitute.For<ICommentService>();
            result.CreateViewModel<IPullRequestReviewCommentViewModel>().Returns(_ =>
                new PullRequestReviewCommentViewModel(commentService));
            return result;
        }

        IPullRequestSession CreateSession()
        {
            var result = Substitute.For<IPullRequestSession>();
            result.User.Returns(new ActorModel { Login = "Viewer" });
            result.RepositoryOwner.Returns("owner");
            result.LocalRepository.CloneUrl.Returns(new UriString("https://github.com/owner/repo"));
            result.LocalRepository.Name.Returns("repo");
            result.LocalRepository.Owner.Returns("shouldnt-be-used");
            result.PullRequest.Returns(new PullRequestDetailModel
            {
                Number = 47,
            });
            return result;
        }
    }
}
