/**
 * Copyright 2023 Y. Meyer-Norwood
 * Copyright 2020 Dan Tulloh
 * Copyright 2016 Carsten Gehling
 *
 * For a full list of contributing authors, see:
 *
 *     https://jirastopwatch.com/contributors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at:
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace StopWatchTest
{
    using Moq;
    using NUnit.Framework;
    using RestSharp;
    using StopWatch;
    using System;
    using System.Collections.Generic;

    [TestFixture]
    public class JiraClientTest
    {
        private Mock<IJiraApiRequestFactory> jiraApiRequestFactoryMock;
        private Mock<IJiraApiRequester> jiraApiRequesterMock;

        private JiraClient jiraClient;


        [SetUp]
        public void Setup()
        {
            jiraApiRequestFactoryMock = new Mock<IJiraApiRequestFactory>();

            jiraApiRequesterMock = new Mock<IJiraApiRequester>();

            jiraClient = new JiraClient(jiraApiRequestFactoryMock.Object, jiraApiRequesterMock.Object);
        }


        [Test, Description("Authenticate returns true on successful authentication")]
        public void Authenticate_OnSuccess_It_Returns_True()
        {
            var jiraConfig = new JiraConfiguration()
            {
                timeTrackingConfiguration = new TimeTrackingConfiguration()
            };
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<JiraConfiguration>(It.IsAny<RestRequest>())).Returns(jiraConfig);
            Assert.That(jiraClient.Authenticate("myuser", "myapitoken"), Is.True);
        }


        [Test, Description("Authenticate returns false on unsuccessful authentication")]
        public void Authenticate_OnFailure_It_Returns_False()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<JiraConfiguration>(It.IsAny<RestRequest>())).Throws<RequestDeniedException>();
            Assert.That(jiraClient.Authenticate("myuser", "myapitoken"), Is.False);
        }


        [Test, Description("ValidateSession: On success it sets SessionValid and returns true")]
        public void ValidateSession_OnSuccess_It_Sets_SessionValid_And_Returns_True()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<object>(It.IsAny<RestRequest>())).Returns(new object());
            Assert.That(jiraClient.ValidateSession(), Is.True);
            Assert.That(jiraClient.SessionValid, Is.True);
        }


        [Test, Description("ValidateSession: On failure it resets SessionValid and returns false")]
        public void ValidateSession_OnFailure_It_Resets_SessionValid_And_Returns_False()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<object>(It.IsAny<RestRequest>())).Throws<RequestDeniedException>();
            Assert.That(jiraClient.ValidateSession(), Is.False);
            Assert.That(jiraClient.SessionValid, Is.False);
        }


        [Test, Description("GetIssueSummary: On success it returns a list of type filter")]
        public void GetIssueSummary_OnSuccess_It_Returns_Issue_Summary()
        {
            Issue returnData = new Issue
            {
                Fields = new IssueFields
                {
                    Summary = "The long dark tea-time of the soul"
                }
            };

            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<Issue>(It.IsAny<RestRequest>())).Returns(returnData);

            Assert.That(jiraClient.GetIssueSummary("DG-42", false), Is.EqualTo(returnData.Fields.Summary));
        }


        [Test, Description("GetIssueSummary: On failure it returns empty string")]
        public void GetIssueSummary_OnFailure_It_Returns_Empty_String()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<Issue>(It.IsAny<RestRequest>())).Throws<RequestDeniedException>();
            Assert.That(jiraClient.GetIssueSummary("DG-42", false), Is.EqualTo(""));
        }


        [Test, Description("GetIssueSummary: When issue is a subtask with a parent summary, it prefixes the parent summary")]
        public void GetIssueSummary_WithParent_It_Returns_Parent_And_Issue_Summary()
        {
            Issue returnData = new Issue
            {
                Fields = new IssueFields
                {
                    Summary = "The long dark tea-time of the soul",
                    IssueType = new IssueTypeFields { Subtask = true },
                    Parent = new ParentFields
                    {
                        Key = "DG-1",
                        Fields = new IssueFields { Summary = "Dirk Gently's Holistic Detective Agency" }
                    }
                }
            };

            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<Issue>(It.IsAny<RestRequest>())).Returns(returnData);

            Assert.That(jiraClient.GetIssueSummary("DG-42", false), Is.EqualTo("Dirk Gently's Holistic Detective Agency / The long dark tea-time of the soul"));
        }


        [Test, Description("GetIssueSummary: When issue is not a subtask but has a parent (e.g. an Epic link), it returns only the issue summary")]
        public void GetIssueSummary_WithParentButNotSubtask_It_Returns_Issue_Summary_Only()
        {
            Issue returnData = new Issue
            {
                Fields = new IssueFields
                {
                    Summary = "The long dark tea-time of the soul",
                    IssueType = new IssueTypeFields { Subtask = false },
                    Parent = new ParentFields
                    {
                        Key = "DG-1",
                        Fields = new IssueFields { Summary = "Dirk Gently's Holistic Detective Agency" }
                    }
                }
            };

            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<Issue>(It.IsAny<RestRequest>())).Returns(returnData);

            Assert.That(jiraClient.GetIssueSummary("DG-42", false), Is.EqualTo(returnData.Fields.Summary));
        }


        [Test, Description("GetIssueSummary: When issue has no parent, it returns only the issue summary")]
        public void GetIssueSummary_WithoutParent_It_Returns_Issue_Summary_Only()
        {
            Issue returnData = new Issue
            {
                Fields = new IssueFields
                {
                    Summary = "The long dark tea-time of the soul",
                    Parent = null
                }
            };

            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<Issue>(It.IsAny<RestRequest>())).Returns(returnData);

            Assert.That(jiraClient.GetIssueSummary("DG-42", false), Is.EqualTo(returnData.Fields.Summary));
        }


        [Test, Description("GetIssueSummary: When parent has no summary, it returns only the issue summary")]
        public void GetIssueSummary_WithParentWithoutSummary_It_Returns_Issue_Summary_Only()
        {
            Issue returnData = new Issue
            {
                Fields = new IssueFields
                {
                    Summary = "The long dark tea-time of the soul",
                    Parent = new ParentFields
                    {
                        Key = "DG-1",
                        Fields = new IssueFields { Summary = null }
                    }
                }
            };

            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<Issue>(It.IsAny<RestRequest>())).Returns(returnData);

            Assert.That(jiraClient.GetIssueSummary("DG-42", false), Is.EqualTo(returnData.Fields.Summary));
        }


        [Test, Description("GetIssueSummary: With parent and addProjectName, project name stays the outermost prefix")]
        public void GetIssueSummary_WithParentAndProjectName_It_Returns_Project_Parent_And_Issue_Summary()
        {
            Issue returnData = new Issue
            {
                Fields = new IssueFields
                {
                    Summary = "The long dark tea-time of the soul",
                    Project = new ProjectFields { Name = "Dirk Gently" },
                    IssueType = new IssueTypeFields { Subtask = true },
                    Parent = new ParentFields
                    {
                        Key = "DG-1",
                        Fields = new IssueFields { Summary = "Dirk Gently's Holistic Detective Agency" }
                    }
                }
            };

            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<Issue>(It.IsAny<RestRequest>())).Returns(returnData);

            Assert.That(jiraClient.GetIssueSummary("DG-42", true), Is.EqualTo("Dirk Gently: Dirk Gently's Holistic Detective Agency / The long dark tea-time of the soul"));
        }

        [Test, Description("GetIssueTimetracking: On success it returns a timetracking object")]
        public void GetIssueTimetracking_OnSuccess_It_Returns_RemainingTime()
        {
            Issue returnData = new Issue
            {
                Fields = new IssueFields
                {
                    Summary = "The long dark tea-time of the soul",
                    Timetracking = new TimetrackingFields
                    {
                        RemainingEstimate = "1h",
                        RemainingEstimateSeconds = 360
                    }
                }
            };

            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<Issue>(It.IsAny<RestRequest>())).Returns(returnData);

            Assert.That(jiraClient.GetIssueTimetracking("DG-42"), Is.EqualTo(returnData.Fields.Timetracking));
        }


        [Test, Description("GetIssueTimetracking: On failure it returns null")]
        public void GetIssueTimetracking_OnFailure_It_Returns_Empty_String()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<Issue>(It.IsAny<RestRequest>())).Throws<RequestDeniedException>();
            Assert.That(jiraClient.GetIssueTimetracking("DG-42"), Is.Null);
        }


        [Test, Description("PostWorklog: On success it returns true")]
        public void PostWorklog_OnSuccess_It_Returns_True()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<object>(It.IsAny<RestRequest>())).Returns(new object());

            Assert.That(jiraClient.PostWorklog("DG-42", DateTimeOffset.UtcNow, new TimeSpan(1, 20, 0), "Time is an illusion", EstimateUpdateMethods.Auto, null), Is.True);
        }


        [Test, Description("PostWorklog: On failure it returns false")]
        public void PostWorklog_OnFailure_It_Returns_False()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<object>(It.IsAny<RestRequest>())).Throws<RequestDeniedException>();
            Assert.That(jiraClient.PostWorklog("DG-42", DateTimeOffset.UtcNow, new TimeSpan(2, 10, 0), "Lunchtime doubly so", EstimateUpdateMethods.Auto, null), Is.False);
        }


        [Test, Description("PostComment: On success it returns true")]
        public void PostComment_OnSuccess_It_Returns_True()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<object>(It.IsAny<RestRequest>())).Returns(new object());

            Assert.That(jiraClient.PostComment("DG-42", "Time is an illusion"), Is.True);
        }


        [Test, Description("PostComment: On failure it returns false")]
        public void PostComment_OnFailure_It_Returns_False()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<object>(It.IsAny<RestRequest>())).Throws<RequestDeniedException>();
            Assert.That(jiraClient.PostComment("DG-42", "Lunchtime doubly so"), Is.False);
        }


        [Test, Description("GetAvailableTransitions: On success it returns a list transitions currently available for issue")]
        public void GetAvailableTransitions_OnSuccess_It_Returns_List_Of_Issues()
        {
            AvailableTransitions returnData = new AvailableTransitions
            {
                Expand = "transitions",
                Transitions = new List<Transition>()

            };
            returnData.Transitions.Add(new Transition { Id = 8, Name = "Trans1" });
            returnData.Transitions.Add(new Transition { Id = 9, Name = "Trans2" });

            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<AvailableTransitions>(It.IsAny<RestRequest>())).Returns(returnData);

            Assert.That(jiraClient.GetAvailableTransitions("KEY-3"), Is.EqualTo(returnData));
        }


        [Test, Description("GetAvailableTransitions: On failure it returns null")]
        public void GetAvailableTransitions_OnFailure_It_Returns_Null()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<AvailableTransitions>(It.IsAny<RestRequest>())).Throws<RequestDeniedException>();
            Assert.That(jiraClient.GetAvailableTransitions("KEY-3"), Is.Null);
        }


        [Test, Description("DoTransition: On success it returns true")]
        public void DoTransition_OnSuccess_It_Returns_True()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<object>(It.IsAny<RestRequest>())).Returns(new object());

            Assert.That(jiraClient.DoTransition("DG-42", 6), Is.True);
        }


        [Test, Description("DoTransition: On failure it returns false")]
        public void DoTransition_OnFailure_It_Returns_False()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<object>(It.IsAny<RestRequest>())).Throws<RequestDeniedException>();
            Assert.That(jiraClient.DoTransition("DG-42", 6), Is.False);
        }


    }
}
