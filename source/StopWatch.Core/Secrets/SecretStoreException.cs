using System;

namespace StopWatch
{
    /// <summary>
    /// The secret store could not read or write a secret. The message is
    /// written for the user; the inner exception, if any, is the technical cause.
    /// </summary>
    public class SecretStoreException : Exception
    {
        public SecretStoreException(string message) : base(message)
        {
        }

        public SecretStoreException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
