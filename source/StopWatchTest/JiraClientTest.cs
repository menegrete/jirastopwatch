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
    using System.Linq;
    using System.Net;

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

            Assert.That(jiraClient.GetIssueSummary("DG-42", false).Summary, Is.EqualTo(returnData.Fields.Summary));
        }


        [Test, Description("GetIssueSummary: On failure it returns an empty result")]
        public void GetIssueSummary_OnFailure_It_Returns_Empty_Result()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<Issue>(It.IsAny<RestRequest>())).Throws<RequestDeniedException>();

            IssueSummaryResult result = jiraClient.GetIssueSummary("DG-42", false);

            Assert.That(result.Summary, Is.EqualTo(""));
            Assert.That(result.ParentKey, Is.EqualTo(""));
        }


        [Test, Description("GetIssueSummary: When issue is a subtask with a parent summary, it prefixes the parent summary and returns the parent key")]
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

            IssueSummaryResult result = jiraClient.GetIssueSummary("DG-42", false);

            Assert.That(result.Summary, Is.EqualTo("Dirk Gently's Holistic Detective Agency / The long dark tea-time of the soul"));
            Assert.That(result.ParentKey, Is.EqualTo("DG-1"));
        }


        [Test, Description("GetIssueSummary: When issue is not a subtask but has a parent (e.g. an Epic link), it returns only the issue summary and no parent key")]
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

            IssueSummaryResult result = jiraClient.GetIssueSummary("DG-42", false);

            Assert.That(result.Summary, Is.EqualTo(returnData.Fields.Summary));
            Assert.That(result.ParentKey, Is.EqualTo(""), "the parent here is an Epic, not something to offer copying");
        }


        [Test, Description("GetIssueSummary: When issue has no parent, it returns only the issue summary and no parent key")]
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

            IssueSummaryResult result = jiraClient.GetIssueSummary("DG-42", false);

            Assert.That(result.Summary, Is.EqualTo(returnData.Fields.Summary));
            Assert.That(result.ParentKey, Is.EqualTo(""));
        }


        [Test, Description("GetIssueSummary: When a subtask's parent has no key, it returns no parent key")]
        public void GetIssueSummary_SubtaskWithoutParent_It_Returns_No_ParentKey()
        {
            Issue returnData = new Issue
            {
                Fields = new IssueFields
                {
                    Summary = "The long dark tea-time of the soul",
                    IssueType = new IssueTypeFields { Subtask = true },
                    Parent = null
                }
            };

            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<Issue>(It.IsAny<RestRequest>())).Returns(returnData);

            Assert.That(jiraClient.GetIssueSummary("DG-42", false).ParentKey, Is.EqualTo(""));
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

            Assert.That(jiraClient.GetIssueSummary("DG-42", false).Summary, Is.EqualTo(returnData.Fields.Summary));
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

            IssueSummaryResult result = jiraClient.GetIssueSummary("DG-42", true);

            Assert.That(result.Summary, Is.EqualTo("Dirk Gently: Dirk Gently's Holistic Detective Agency / The long dark tea-time of the soul"));
            Assert.That(result.ParentKey, Is.EqualTo("DG-1"));
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


        [Test, Description("PostWorklog: On success it returns the id of the worklog Jira created")]
        public void PostWorklog_OnSuccess_It_Returns_The_Worklog_Id()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<CreatedWorklog>(It.IsAny<RestRequest>())).Returns(new CreatedWorklog { Id = "10042" });

            var result = jiraClient.PostWorklog("DG-42", DateTimeOffset.UtcNow, new TimeSpan(1, 20, 0), "Time is an illusion", EstimateUpdateMethods.Auto, null);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Value, Is.EqualTo("10042"));
        }


        [Test, Description("PostWorklog: On failure it reports the reason and Jira's message")]
        public void PostWorklog_OnFailure_It_Reports_Reason_And_Message()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<CreatedWorklog>(It.IsAny<RestRequest>()))
                .Throws(new RequestDeniedException("denied", HttpStatusCode.BadRequest, "{\"errorMessages\":[\"Issue is closed\"],\"errors\":{}}", null));

            var result = jiraClient.PostWorklog("DG-42", DateTimeOffset.UtcNow, new TimeSpan(2, 10, 0), "Lunchtime doubly so", EstimateUpdateMethods.Auto, null);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Reason, Is.EqualTo(JiraFailureReason.Validation));
            Assert.That(result.Message, Is.EqualTo("Issue is closed"));
        }


        [TestCase(HttpStatusCode.Unauthorized, "Unauthorized")]
        [TestCase(HttpStatusCode.Forbidden, "Forbidden")]
        [TestCase(HttpStatusCode.NotFound, "NotFound")]
        [TestCase(HttpStatusCode.BadRequest, "Validation")]
        [TestCase(HttpStatusCode.InternalServerError, "Unknown")]
        [TestCase((HttpStatusCode)0, "Network")]
        [Description("PostWorklog: each HTTP status maps to its own reason")]
        public void PostWorklog_Maps_Status_To_Reason(HttpStatusCode status, string expected)
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<CreatedWorklog>(It.IsAny<RestRequest>()))
                .Throws(new RequestDeniedException("denied", status, "", null));

            Assert.That(jiraClient.PostWorklog("DG-42", DateTimeOffset.UtcNow, TimeSpan.FromHours(1), "", EstimateUpdateMethods.Auto, null).Reason.ToString(), Is.EqualTo(expected));
        }


        [Test, Description("PostWorklog: a dropped connection reports the network error as the message")]
        public void PostWorklog_OnNetworkFailure_It_Reports_The_Inner_Message()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<CreatedWorklog>(It.IsAny<RestRequest>()))
                .Throws(new RequestDeniedException("failed", 0, null, new Exception("name not resolved")));

            var result = jiraClient.PostWorklog("DG-42", DateTimeOffset.UtcNow, TimeSpan.FromHours(1), "", EstimateUpdateMethods.Auto, null);

            Assert.That(result.Reason, Is.EqualTo(JiraFailureReason.Network));
            Assert.That(result.Message, Is.EqualTo("name not resolved"));
        }


        [Test, Description("PostWorklog: without credentials it fails as unauthorized")]
        public void PostWorklog_WithoutCredentials_It_Is_Unauthorized()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<CreatedWorklog>(It.IsAny<RestRequest>())).Throws<UsernameAndApiTokenNotSetException>();

            var result = jiraClient.PostWorklog("DG-42", DateTimeOffset.UtcNow, TimeSpan.FromHours(1), "", EstimateUpdateMethods.Auto, null);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Reason, Is.EqualTo(JiraFailureReason.Unauthorized));
        }


        [Test, Description("PostComment: On success it succeeds")]
        public void PostComment_OnSuccess_It_Succeeds()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<object>(It.IsAny<RestRequest>())).Returns(new object());

            Assert.That(jiraClient.PostComment("DG-42", "Time is an illusion").Success, Is.True);
        }


        [Test, Description("PostComment: On a permission failure it reports Forbidden and Jira's message")]
        public void PostComment_OnFailure_It_Reports_Forbidden()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<object>(It.IsAny<RestRequest>()))
                .Throws(new RequestDeniedException("denied", HttpStatusCode.Forbidden, "{\"errorMessages\":[\"You do not have permission to comment\"]}", null));

            var result = jiraClient.PostComment("DG-42", "Lunchtime doubly so");

            Assert.That(result.Success, Is.False);
            Assert.That(result.Reason, Is.EqualTo(JiraFailureReason.Forbidden));
            Assert.That(result.Message, Is.EqualTo("You do not have permission to comment"));
        }


        private static Issue SearchHit(string key, string summary, string parentKey = null)
        {
            return new Issue
            {
                Key = key,
                Fields = new IssueFields
                {
                    Summary = summary,
                    IssueType = new IssueTypeFields { Id = "10003", Name = "Sub-task", Subtask = parentKey != null },
                    Parent = parentKey == null ? null : new ParentFields { Key = parentKey, Fields = new IssueFields { Summary = "Padre" } },
                    Project = new ProjectFields { Key = "DG", Name = "Douglas" },
                    Status = new StatusFields { Name = "In Progress" }
                }
            };
        }


        [Test, Description("SearchIssues: It maps the fields of each hit")]
        public void SearchIssues_OnSuccess_It_Maps_Issues()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<SearchResults>(It.IsAny<RestRequest>()))
                .Returns(new SearchResults { Issues = { SearchHit("DG-43", "Desarrollo", "DG-42") } });

            var result = jiraClient.SearchIssues("project = DG");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Value, Has.Count.EqualTo(1));
            var issue = result.Value[0];
            Assert.That(issue.Key, Is.EqualTo("DG-43"));
            Assert.That(issue.Summary, Is.EqualTo("Desarrollo"));
            Assert.That(issue.IssueTypeId, Is.EqualTo("10003"));
            Assert.That(issue.IsSubtask, Is.True);
            Assert.That(issue.ParentKey, Is.EqualTo("DG-42"));
            Assert.That(issue.ProjectKey, Is.EqualTo("DG"));
            Assert.That(issue.Status, Is.EqualTo("In Progress"));
        }


        [Test, Description("SearchIssues: It follows the page token until the last page")]
        public void SearchIssues_It_Reads_All_Pages()
        {
            jiraApiRequestFactoryMock.Setup(f => f.CreateSearchIssuesRequest("project = DG", null)).Returns(new RestRequest("first"));
            jiraApiRequestFactoryMock.Setup(f => f.CreateSearchIssuesRequest("project = DG", "tok1")).Returns(new RestRequest("second"));
            jiraApiRequestFactoryMock.Setup(f => f.CreateSearchIssuesRequest("project = DG", "tok2")).Returns(new RestRequest("third"));
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<SearchResults>(It.Is<RestRequest>(r => r.Resource == "first")))
                .Returns(new SearchResults { Issues = { SearchHit("DG-1", "a") }, NextPageToken = "tok1" });
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<SearchResults>(It.Is<RestRequest>(r => r.Resource == "second")))
                .Returns(new SearchResults { Issues = { SearchHit("DG-2", "b") }, NextPageToken = "tok2" });
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<SearchResults>(It.Is<RestRequest>(r => r.Resource == "third")))
                .Returns(new SearchResults { Issues = { SearchHit("DG-3", "c") } });

            var result = jiraClient.SearchIssues("project = DG");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Value.Select(i => i.Key), Is.EqualTo(new[] { "DG-1", "DG-2", "DG-3" }));
        }


        [Test, Description("SearchIssues: No matches is an empty list, not an error")]
        public void SearchIssues_Without_Matches_It_Returns_Empty_List()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<SearchResults>(It.IsAny<RestRequest>())).Returns(new SearchResults());

            var result = jiraClient.SearchIssues("project = NOPE");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Value, Is.Empty);
        }


        [Test, Description("SearchIssues: A permission failure is reported, not mistaken for no matches")]
        public void SearchIssues_OnPermissionFailure_It_Reports_The_Reason()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<SearchResults>(It.IsAny<RestRequest>()))
                .Throws(new RequestDeniedException("denied", HttpStatusCode.Forbidden, "", null));

            var result = jiraClient.SearchIssues("project = DG");

            Assert.That(result.Success, Is.False);
            Assert.That(result.Reason, Is.EqualTo(JiraFailureReason.Forbidden));
        }


        [Test, Description("SearchIssues: A network failure is reported as such")]
        public void SearchIssues_OnNetworkFailure_It_Reports_Network()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<SearchResults>(It.IsAny<RestRequest>()))
                .Throws(new RequestDeniedException("failed", 0, null, new Exception("timeout")));

            Assert.That(jiraClient.SearchIssues("project = DG").Reason, Is.EqualTo(JiraFailureReason.Network));
        }


        [Test, Description("SearchIssues: Without credentials it fails as unauthorized")]
        public void SearchIssues_WithoutCredentials_It_Is_Unauthorized()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<SearchResults>(It.IsAny<RestRequest>())).Throws<UsernameAndApiTokenNotSetException>();

            Assert.That(jiraClient.SearchIssues("project = DG").Reason, Is.EqualTo(JiraFailureReason.Unauthorized));
        }


        [Test, Description("GetSubtasks: It searches by parent and returns the summary exactly as Jira has it")]
        public void GetSubtasks_It_Returns_The_Raw_Summary()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<SearchResults>(It.IsAny<RestRequest>()))
                .Returns(new SearchResults { Issues = { SearchHit("DG-43", "Desarrollo", "DG-42") } });

            var result = jiraClient.GetSubtasks("DG-42");

            jiraApiRequestFactoryMock.Verify(f => f.CreateSearchIssuesRequest("parent = \"DG-42\"", null), Times.Once);
            Assert.That(result.Value.Single().Summary, Is.EqualTo("Desarrollo"));
        }


        [Test, Description("GetSubtasks: A parent without subtasks gives an empty list")]
        public void GetSubtasks_Without_Subtasks_It_Returns_Empty_List()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<SearchResults>(It.IsAny<RestRequest>())).Returns(new SearchResults());

            var result = jiraClient.GetSubtasks("DG-42");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Value, Is.Empty);
        }


        [Test, Description("GetSubtaskTypes: It returns only the subtask types of the project")]
        public void GetSubtaskTypes_It_Returns_Only_Subtask_Types()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<ProjectDetails>(It.IsAny<RestRequest>()))
                .Returns(new ProjectDetails
                {
                    IssueTypes =
                    {
                        new IssueTypeFields { Id = "1", Name = "Task" },
                        new IssueTypeFields { Id = "5", Name = "Sub-task", Subtask = true },
                        new IssueTypeFields { Id = "6", Name = "Sub-bug", Subtask = true }
                    }
                });

            var result = jiraClient.GetSubtaskTypes("DG");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Value.Select(t => t.Id), Is.EqualTo(new[] { "5", "6" }));
            Assert.That(result.Value[0].Name, Is.EqualTo("Sub-task"));
        }


        [Test, Description("GetSubtaskTypes: A project without subtask types gives an empty list, not an error")]
        public void GetSubtaskTypes_Without_Subtask_Types_It_Returns_Empty_List()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<ProjectDetails>(It.IsAny<RestRequest>()))
                .Returns(new ProjectDetails { IssueTypes = { new IssueTypeFields { Id = "1", Name = "Task" } } });

            var result = jiraClient.GetSubtaskTypes("DG");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Value, Is.Empty);
        }


        [Test, Description("GetSubtaskTypes: An unknown project is reported as not found")]
        public void GetSubtaskTypes_OnUnknownProject_It_Reports_NotFound()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<ProjectDetails>(It.IsAny<RestRequest>()))
                .Throws(new RequestDeniedException("nf", HttpStatusCode.NotFound, "", null));

            Assert.That(jiraClient.GetSubtaskTypes("NOPE").Reason, Is.EqualTo(JiraFailureReason.NotFound));
        }


        [Test, Description("CreateSubtask: It creates under the project of the parent and returns the new key")]
        public void CreateSubtask_OnSuccess_It_Returns_The_Key()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<CreatedIssue>(It.IsAny<RestRequest>())).Returns(new CreatedIssue { Key = "DG-99" });

            var result = jiraClient.CreateSubtask("DG-42", "Desarrollo", "5");

            jiraApiRequestFactoryMock.Verify(f => f.CreateCreateSubtaskRequest("DG", "DG-42", "Desarrollo", "5"), Times.Once);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Value, Is.EqualTo("DG-99"));
        }


        [Test, Description("CreateSubtask: Required fields the project adds are named in the failure")]
        public void CreateSubtask_WithRequiredFields_It_Names_Each_Missing_Field()
        {
            string body = "{\"errorMessages\":[],\"errors\":{\"customfield_10050\":\"Team is required.\",\"components\":\"Component/s is required.\"}}";
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<CreatedIssue>(It.IsAny<RestRequest>()))
                .Throws(new RequestDeniedException("bad", HttpStatusCode.BadRequest, body, null));

            var result = jiraClient.CreateSubtask("DG-42", "Desarrollo", "5");

            Assert.That(result.Success, Is.False);
            Assert.That(result.Reason, Is.EqualTo(JiraFailureReason.Validation));
            Assert.That(result.Message, Is.EqualTo("customfield_10050: Team is required.; components: Component/s is required."));
        }


        [Test, Description("CreateSubtask: An issue type that is not a subtask of the project is a validation failure")]
        public void CreateSubtask_WithInvalidType_It_Reports_Validation()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<CreatedIssue>(It.IsAny<RestRequest>()))
                .Throws(new RequestDeniedException("bad", HttpStatusCode.BadRequest, "{\"errors\":{\"issuetype\":\"The issue type selected is invalid.\"}}", null));

            var result = jiraClient.CreateSubtask("DG-42", "Desarrollo", "999");

            Assert.That(result.Reason, Is.EqualTo(JiraFailureReason.Validation));
            Assert.That(result.Message, Is.EqualTo("issuetype: The issue type selected is invalid."));
        }


        [Test, Description("CreateSubtask: Without permission to create it reports Forbidden")]
        public void CreateSubtask_WithoutPermission_It_Reports_Forbidden()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<CreatedIssue>(It.IsAny<RestRequest>()))
                .Throws(new RequestDeniedException("denied", HttpStatusCode.Forbidden, "{\"errorMessages\":[\"You do not have permission to create issues\"]}", null));

            var result = jiraClient.CreateSubtask("DG-42", "Desarrollo", "5");

            Assert.That(result.Reason, Is.EqualTo(JiraFailureReason.Forbidden));
            Assert.That(result.Message, Is.EqualTo("You do not have permission to create issues"));
        }


        [Test, Description("CreateSubtask: A parent that does not exist is reported as not found")]
        public void CreateSubtask_WithUnknownParent_It_Reports_NotFound()
        {
            jiraApiRequesterMock.Setup(m => m.DoAuthenticatedRequest<CreatedIssue>(It.IsAny<RestRequest>()))
                .Throws(new RequestDeniedException("nf", HttpStatusCode.NotFound, "", null));

            Assert.That(jiraClient.CreateSubtask("DG-404", "Desarrollo", "5").Reason, Is.EqualTo(JiraFailureReason.NotFound));
        }


        [Test, Description("CreateSubtask: Something that is not an issue key fails without calling Jira")]
        public void CreateSubtask_WithMalformedParentKey_It_Fails_Without_A_Request()
        {
            var result = jiraClient.CreateSubtask("nonsense", "Desarrollo", "5");

            Assert.That(result.Reason, Is.EqualTo(JiraFailureReason.Validation));
            jiraApiRequesterMock.Verify(m => m.DoAuthenticatedRequest<CreatedIssue>(It.IsAny<RestRequest>()), Times.Never);
        }


        [Test, Description("ParseJiraMessage: A body that is not Jira's error shape gives no message")]
        public void ParseJiraMessage_WithOtherContent_It_Returns_Empty()
        {
            Assert.That(JiraErrors.ParseJiraMessage("<html>Bad gateway</html>"), Is.Empty);
            Assert.That(JiraErrors.ParseJiraMessage(null), Is.Empty);
            Assert.That(JiraErrors.ParseJiraMessage("[1,2]"), Is.Empty);
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
