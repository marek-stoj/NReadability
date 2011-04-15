/*
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
using System.IO;
using System.Reflection;
using SysConsole = System.Console;

namespace NReadability.Console
{
  internal class Program
  {
    #region Application entry point

    private static void Main(string[] args)
    {
      if (args == null || args.Length != 2)
      {
        DisplayUsage();
        Environment.Exit(1);
      }

      string inputFile = args[0];
      string outputFile = args[1];

      var nReadabilityTranscoder = new NReadabilityTranscoder();
      bool mainContentExtracted;

      File.WriteAllText(
        outputFile,
        nReadabilityTranscoder.Transcode(File.ReadAllText(inputFile), out mainContentExtracted));
    }

    #endregion

    #region Private helper methods

    private static void DisplayUsage()
    {
      SysConsole.WriteLine(
        "Usage: {0} inputFile outputFile",
        Path.GetFileName(Assembly.GetExecutingAssembly().CodeBase));
    }

    #endregion
  }
}
