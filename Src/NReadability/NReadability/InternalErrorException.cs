﻿/*
 * NReadability
 * http://code.google.com/p/nreadability/
 * 
 * Copyright 2010 Marek Stój
 * http://immortal.pl/
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Runtime.Serialization;

namespace NReadability
{
  /// <summary>
  /// An exception that is thrown when an internal error occurrs in the application.
  /// Internal error in the application means that there is a bug in the application.
  /// </summary>
  [Serializable]
  public class InternalErrorException : Exception
  {
    #region Constructor(s)

    /// <summary>
    /// Initializes a new instance of the InternalErrorException class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
    public InternalErrorException(string message, Exception innerException)
      : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the InternalErrorException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public InternalErrorException(string message)
      : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the InternalErrorException class.
    /// </summary>
    public InternalErrorException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the InternalErrorException class with serialized data.
    /// </summary>
    /// <param name="info">The System.Runtime.Serialization.SerializationInfo that holds the serialized object data about the exception being thrown.</param>
    /// <param name="context">The System.Runtime.Serialization.StreamingContext that contains contextual information about the source or destination.</param>
    protected InternalErrorException(SerializationInfo info, StreamingContext context)
      : base(info, context)
    {
    }

    #endregion
  }
}
