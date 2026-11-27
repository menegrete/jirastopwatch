using System.Collections.Generic;
using System.Net;
using System.Text.Json;

namespace StopWatch
{
    /// <summary>Why a Jira call failed, as far as the response lets us tell.</summary>
    internal enum JiraFailureReason
    {
        None,
        Unauthorized,
        Forbidden,
        NotFound,

        /// <summary>Jira refused the content: a closed issue, a missing required field, a bad issue type.</summary>
        Validation,

        /// <summary>No response at all: DNS, connection, timeout.</summary>
        Network,
        Unknown
    }


    /// <summary>The outcome of a Jira call that has nothing to return but whether it worked.</summary>
    internal class JiraResult
    {
        public bool Success { get; protected set; }

        public JiraFailureReason Reason { get; protected set; }

        /// <summary>Jira's own explanation of the failure; empty on success.</summary>
        public string Message { get; protected set; } = "";

        public static JiraResult Ok()
        {
            return new JiraResult { Success = true };
        }

        public static JiraResult Fail(JiraFailureReason reason, string message)
        {
            return new JiraResult { Success = false, Reason = reason, Message = message ?? "" };
        }
    }


    /// <summary>
    /// The outcome of a Jira call that returns something, such as the id of
    /// the worklog it created. <see cref="Value"/> is only meaningful on success.
    /// </summary>
    internal class JiraResult<T> : JiraResult
    {
        public T Value { get; private set; }

        public static JiraResult<T> Ok(T value)
        {
            return new JiraResult<T> { Success = true, Value = value };
        }

        public new static JiraResult<T> Fail(JiraFailureReason reason, string message)
        {
            return new JiraResult<T> { Success = false, Reason = reason, Message = message ?? "" };
        }
    }


    /// <summary>Turns a failed request into a <see cref="JiraFailureReason"/> and a message a person can read.</summary>
    internal static class JiraErrors
    {
        public static JiraFailureReason ReasonFor(RequestDeniedException ex)
        {
            switch (ex.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                    return JiraFailureReason.Unauthorized;
                case HttpStatusCode.Forbidden:
                    return JiraFailureReason.Forbidden;
                case HttpStatusCode.NotFound:
                    return JiraFailureReason.NotFound;
                case HttpStatusCode.BadRequest:
                    return JiraFailureReason.Validation;
                case 0:
                    return JiraFailureReason.Network;
                default:
                    return JiraFailureReason.Unknown;
            }
        }


        public static string MessageFor(RequestDeniedException ex)
        {
            if (ex.StatusCode == 0)
                return ex.InnerException?.Message ?? ex.Message;

            string fromJira = ParseJiraMessage(ex.ResponseContent);
            return string.IsNullOrEmpty(fromJira) ? ex.Message : fromJira;
        }


        /// <summary>
        /// Reads Jira's error body, <c>{"errorMessages":[..],"errors":{"field":"msg"}}</c>,
        /// into one line: the general messages first, then "field: msg" for each
        /// field. Returns empty when the body is not that shape.
        /// </summary>
        public static string ParseJiraMessage(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return "";

            var parts = new List<string>();
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(content))
                {
                    if (doc.RootElement.ValueKind != JsonValueKind.Object)
                        return "";

                    if (doc.RootElement.TryGetProperty("errorMessages", out JsonElement messages) && messages.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement message in messages.EnumerateArray())
                        {
                            if (message.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(message.GetString()))
                                parts.Add(message.GetString());
                        }
                    }

                    if (doc.RootElement.TryGetProperty("errors", out JsonElement errors) && errors.ValueKind == JsonValueKind.Object)
                    {
                        foreach (JsonProperty error in errors.EnumerateObject())
                        {
                            if (error.Value.ValueKind == JsonValueKind.String)
                                parts.Add(error.Name + ": " + error.Value.GetString());
                        }
                    }
                }
            }
            catch (JsonException)
            {
                return "";
            }

            return string.Join("; ", parts);
        }
    }
}
