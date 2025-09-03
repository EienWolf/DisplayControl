using System;
using System.Runtime.Serialization;

namespace DisplayControl.Abstractions
{
    /// <summary>
    /// Application-specific exception for Display Control operations.
    /// Use this type to signal domain errors rather than throwing generic exceptions.
    /// </summary>
    [Serializable]
    public class DisplayControlException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DisplayControlException"/> class.
        /// </summary>
        public DisplayControlException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DisplayControlException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        public DisplayControlException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DisplayControlException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public DisplayControlException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DisplayControlException"/> class with serialized data.
        /// </summary>
        /// <param name="info">The serialization information object holding the serialized object data.</param>
        /// <param name="context">The streaming context that contains contextual information about the source or destination.</param>
        protected DisplayControlException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}

