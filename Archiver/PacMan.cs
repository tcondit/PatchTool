﻿using Microsoft.Test.CommandLineParsing;
using System;
using System.IO;
using System.Windows.Forms;

// using command-line parser from TestAPI - http://testapi.codeplex.com/

namespace PatchTool
{
    public class PacMan
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                string usage = "PacMan.exe\n\n";
                usage += "Required arguments:\n";
                usage += "\t-sourceDir=<sourceDir>";
                usage += "\t-appName=<appName>";
                usage += "\n\n";
                usage += "Optional arguments:\n";
                usage += "\t-patchID=<patchID>\n";
                usage += "\t-productVersion=<productVersion>\n";
                // TC: this doesn't do anything yet
                usage += "\t-?\n";

                MessageBox.Show(usage, "PacMan needs more info");
                return;
            }

            CommandLineDictionary d = CommandLineDictionary.FromArguments(args, '-', '=');
            Archiver a = new Archiver();

            // path to the affected files (in other words, the patch contents)
            string src_dir;
            // application identifier: ServerSuite now, then ChannelManager, then others (LATER)
            //string app_id;
            // name of the published file (SFX)
            string patch_id;
            string product_version;

            if (d.ContainsKey("sourceDir"))
            {
                d.TryGetValue("sourceDir", out src_dir);
                a.SourceDir = src_dir;
            }
            else
            {
                // "pretty it up" and exit
                throw new ArgumentException("something's broken!");
            }

            if (d.ContainsKey("patchID"))
            {
                d.TryGetValue("patchID", out patch_id);
                a.PatchID = patch_id;
            }
            else
            {
                Console.WriteLine("Warning: using default patch ID: {0}", a.PatchID);
            }
            if (d.ContainsKey("productVersion"))
            {
                d.TryGetValue("productVersion", out product_version);
                a.ProductVersion = product_version;
            }
            else
            {
                Console.WriteLine("Warning: using default product version: {0}", a.ProductVersion);
            }

            // TC: there's no way to know the "real" extract dir ahead of time, since we get the
            // customer's APPDIR from their registry.  Maybe I can add a relocate method to Clyde.

            // TC: ugly workaround
            Guid g = Guid.NewGuid();
            // TC: debug
            //Console.WriteLine(g.ToString());
            a.ExtractDir = Path.Combine(@"C:\patches", g.ToString());
            a.run();
        }
    }
}
