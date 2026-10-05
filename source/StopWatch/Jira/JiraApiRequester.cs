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

using RestSharp;
using StopWatch.Logging;
using System;
using System.Net;

namespace StopWatch
{
    internal class JiraApiRequester : IJiraApiRequester
    {
        public string ErrorMessage { get; private set; }

        public JiraApiRequester(IRestClientFactory restClientFactory, IJiraApiRequestFactory jiraApiRequestFactory)
        {
            this.restClientFactory = restClientFactory;
            this.jiraApiRequestFactory = jiraApiRequestFactory;
            ErrorMessage = "";
        }

        public T DoAuthenticatedRequest<T>(RestRequest request)
            where T : new()
        {
            AddAuthHeader(request);

            IRestClient client = restClientFactory.Create();

            _logger.Log(string.Format("Request: {0}", client.BuildUri(request)));
            RestResponse<T> response = client.Execute<T>(request);
            _logger.Log(string.Format("Response: {0} - {1}", response.StatusCode, StringHelpers.Truncate(response.Content, 100)));

            ThrowIfFailed(response);

            ErrorMessage = "";
            return response.Data;
        }

        /// <summary>
        /// Throws <see cref="RequestDeniedException"/> carrying the status and
        /// body of any response that is not a success, so that callers can tell
        /// a 401 from a 400 or a dropped connection.
        /// </summary>
        internal void ThrowIfFailed(RestResponse response)
        {
            if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created)
                return;

            // 401/400 leave ErrorMessage untouched, as they always have.
            if (response.StatusCode != HttpStatusCode.Unauthorized && response.StatusCode != HttpStatusCode.BadRequest)
                ErrorMessage = response.ErrorMessage;

            string message = string.IsNullOrEmpty(response.ErrorMessage)
                ? string.Format("Jira responded with HTTP {0}", (int)response.StatusCode)
                : response.ErrorMessage;

            throw new RequestDeniedException(message, response.StatusCode, response.Content, response.ErrorException);
        }

        public void SetAuthentication(string username, string apiToken)
        {
            _username = username;
            _apiToken = apiToken;
        }

        private void AddAuthHeader(RestRequest request)
        {
            if (string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_apiToken))
            {
                throw new UsernameAndApiTokenNotSetException();
            }
            request.AddHeader("Authorization", "Basic " + System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_username}:{_apiToken}")));
        }

        private Logger _logger = Logger.Instance;

        private IRestClientFactory restClientFactory;
        private IJiraApiRequestFactory jiraApiRequestFactory;
        private string _username;
        private string _apiToken;
    }

    internal class RequestDeniedException : Exception
    {
        public RequestDeniedException() : base()
        {
        }

        public RequestDeniedException(string message) : base(message)
        {
        }

        public RequestDeniedException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public RequestDeniedException(string message, HttpStatusCode statusCode, string responseContent, Exception innerException) : base(message, innerException)
        {
            StatusCode = statusCode;
            ResponseContent = responseContent;
        }

        /// <summary>The HTTP status Jira answered with, or 0 when there was no response at all.</summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>The raw body of the failed response, which is where Jira explains what was wrong.</summary>
        public string ResponseContent { get; }
    }

    internal class UsernameAndApiTokenNotSetException : Exception
    {
        public UsernameAndApiTokenNotSetException() : base()
        {
        }

        public UsernameAndApiTokenNotSetException(string message) : base(message)
        {
        }

        public UsernameAndApiTokenNotSetException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
