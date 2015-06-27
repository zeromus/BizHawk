﻿using System;
using System.Text.RegularExpressions;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace BizHawk.Build.Tool
{
	class Program
	{
		static void Main(string[] args)
		{
			string cmd = args[0];
			string[] cmdArgs = Crop(args, 1);
			switch (cmd.ToUpperInvariant())
			{
				case "SVN_REV": SVN_REV(true,cmdArgs); break;
				case "GIT_REV": SVN_REV(false,cmdArgs); break;
			}
		}

		static string[] Crop(string[] arr, int from)
		{
			return arr.Reverse().Take(arr.Length - from).Reverse().ToArray();
		}

		static string EscapeArgument(string s)
		{
			return "\"" + Regex.Replace(s, @"(\\+)$", @"$1$1") + "\"";
		}

		static string RunTool(string path, params string[] args)
		{
			string args_combined = "";
			foreach (var a in args)
				args_combined += EscapeArgument(a) + " ";
			args_combined.TrimEnd(' ');

			var psi = new System.Diagnostics.ProcessStartInfo();
			psi.Arguments = args_combined;
			psi.FileName = path;
			psi.UseShellExecute = false;
			psi.CreateNoWindow = true;
			psi.RedirectStandardOutput = true;
			var proc = new System.Diagnostics.Process();
			proc.StartInfo = psi;
			proc.Start();

			string output = "";
			while (!proc.StandardOutput.EndOfStream)
			{
				output += proc.StandardOutput.ReadLine();
				// do something with line
			}

			return output;
		}

		static void WriteTextIfChanged(string path, string content)
		{
			if (!File.Exists(path))
				goto WRITE;
			string old = File.ReadAllText(path);
			if (old == content) return;
		WRITE:
			File.WriteAllText(path, content);
		}

		//gets the working copy version. use this command:
		//BizHawk.Build.Tool.exe SCM_REV --wc c:\path\to\wcdir --template c:\path\to\templatefile --out c:\path\to\outputfile.cs
		//if the required tools aren't found 
		static void SVN_REV(bool svn, string[] args)
		{
			string wcdir = null, templatefile = null, outfile = null;
			int idx=0;
			while (idx < args.Length)
			{
				string a = args[idx++];
				string au = a.ToUpperInvariant();
				if(au == "--WC")
					wcdir = args[idx++];
				if(au == "--TEMPLATE")
					templatefile = args[idx++];
				if (au == "--OUT")
					outfile = args[idx++];
			}

			//first read the template
			string templateContents = File.ReadAllText(templatefile);

			//pick revision 0 in case the WC investigation fails
			int rev = 0;

			//pick branch unnamed in case investigation fails (or isnt git)
			string branch = "";

			//pick no hash in case investigation fails (or isnt git)
			string shorthash = "";

			//try to find an SVN or GIT and run it
			if (svn)
			{
				string svntool = FileLocator.LocateTool("svnversion");
				if (svntool != "")
				{
					try
					{
						string output = RunTool(svntool, wcdir);
						var parts = output.Split(':');
						var rstr = parts[parts.Length - 1];
						rstr = Regex.Replace(rstr, "[^0-9]", "");
						rev = int.Parse(rstr);
					}
					catch (Exception ex)
					{
						Console.WriteLine(ex);
					}
				}
			}
			else
			{
				string gittool = FileLocator.LocateTool("git");
				if (gittool != "")
				{
					try
					{
						string output = RunTool(gittool, "-C", wcdir, "rev-list", "HEAD", "--count");
						if(int.TryParse(output, out rev))
						{
							output = RunTool(gittool, "-C", wcdir, "rev-parse", "--abbrev-ref", "HEAD");
							if(output.StartsWith("fatal")) {}
							else branch = output;

							output = RunTool(gittool, "-C", wcdir, "log", "-1", "--format=\"%h\"");
							if (output.StartsWith("fatal")) { }
							else shorthash = output;
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine(ex);
					}
				}
			}

			//replace the template and dump the results if needed
			templateContents = templateContents.Replace("$WCREV$", rev.ToString());
			templateContents = templateContents.Replace("$WCBRANCH$", branch);
			templateContents = templateContents.Replace("$WCSHORTHASH$", shorthash);
			WriteTextIfChanged(outfile, templateContents);
		}
	}
}
