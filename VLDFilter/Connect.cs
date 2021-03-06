using System;
using Extensibility;
using EnvDTE;
using EnvDTE80;
using System.Windows.Forms;
using Microsoft.VisualStudio.VCProjectEngine;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VLDFilter
{
	/// <summary>The object for implementing an Add-in.</summary>
	/// <seealso class='IDTExtensibility2' />
	public class Connect : IDTExtensibility2
	{
		/// <summary>Implements the constructor for the Add-in object. Place your initialization code within this method.</summary>
		public Connect()
		{
            shouldTrack = false;
		}

		/// <summary>Implements the OnConnection method of the IDTExtensibility2 interface. Receives notification that the Add-in is being loaded.</summary>
		/// <param term='application'>Root object of the host application.</param>
		/// <param term='connectMode'>Describes how the Add-in is being loaded.</param>
		/// <param term='addInInst'>Object representing this Add-in.</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnConnection(object application, ext_ConnectMode connectMode, object addInInst, ref Array custom)
		{
			_applicationObject = (DTE2)application;
			_addInInstance = (AddIn)addInInst;
            //MessageBox.Show("Hello");

            OutputWindow outputWindow = (OutputWindow)_applicationObject.Windows.Item(Constants.vsWindowKindOutput).Object;
            outputWindowPane = outputWindow.OutputWindowPanes.Item("Debug");
            //outputWindowPane.OutputString("Hello World!");

            debugEvents = _applicationObject.Events.DebuggerEvents;
            debugEvents.OnEnterRunMode += OnEnterRunMode;
            debugEvents.OnEnterDesignMode += OnEnterDesignMode;
            
		}

		/// <summary>Implements the OnDisconnection method of the IDTExtensibility2 interface. Receives notification that the Add-in is being unloaded.</summary>
		/// <param term='disconnectMode'>Describes how the Add-in is being unloaded.</param>
		/// <param term='custom'>Array of parameters that are host application specific.</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnDisconnection(ext_DisconnectMode disconnectMode, ref Array custom)
		{
            //debugEvents.OnEnterRunMode -= OnEnterRunMode;
            //debugEvents.OnEnterDesignMode -= OnEnterDesignMode;
		}

		/// <summary>Implements the OnAddInsUpdate method of the IDTExtensibility2 interface. Receives notification when the collection of Add-ins has changed.</summary>
		/// <param term='custom'>Array of parameters that are host application specific.</param>
		/// <seealso class='IDTExtensibility2' />		
		public void OnAddInsUpdate(ref Array custom)
		{
		}

		/// <summary>Implements the OnStartupComplete method of the IDTExtensibility2 interface. Receives notification that the host application has completed loading.</summary>
		/// <param term='custom'>Array of parameters that are host application specific.</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnStartupComplete(ref Array custom)
		{
		}

		/// <summary>Implements the OnBeginShutdown method of the IDTExtensibility2 interface. Receives notification that the host application is being unloaded.</summary>
		/// <param term='custom'>Array of parameters that are host application specific.</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnBeginShutdown(ref Array custom)
		{
		}

        public void OnEnterRunMode(dbgEventReason Reason)
        {
            if (Reason == dbgEventReason.dbgEventReasonLaunchProgram)
            {
                //MessageBox.Show("Hello");
                startupProj = GetStartupSolnProject(_applicationObject);
                //Output("##Loaded" + proj.CodeModel.Language + "\n");
                if (IsVCProject(startupProj))
                {
                    shouldTrack = true;
                    leakReportPath = Path.Combine(GetWorkingFolder(startupProj), "memory_leak_report.txt");
                    Output("############ Visual Leak Detector Filter started\n");
                    if (File.Exists(leakReportPath))
                    {
                        try
                        {
                            File.Delete(leakReportPath);
                            Output("############ Delete " + leakReportPath + "\n");
                        }
                        catch (System.Exception ex)
                        {
                            OutputException(ex);
                        }
                    }
                    return;
                }
            }
            shouldTrack = false;
            //MessageBox.Show("Start debugging");
        }

        public void OnEnterDesignMode(dbgEventReason Reason)
        {
            if (Reason == dbgEventReason.dbgEventReasonEndProgram)
            {
                if (shouldTrack)
                {
                    if (File.Exists(leakReportPath))
                    {
                        string filterPath = GetVLDFilterFile(startupProj);
                        string output = FilterVLDOutput(leakReportPath, filterPath);
                        Output("\n\n" + output);
                        OutputFullFileToPane(leakReportPath);
                    }
                    else
                    {
                        Output("############ VLD report file not detected\n");
                    }
                }
            }
        }

        public void Output(string msg)
        {
            outputWindowPane.OutputString(msg);
        }

        public void OutputException(Exception ex)
        {
            Output(ex.Message + "\n");
        }

        public Project FindProject(string name, Project proj)
        {
            //Debug.WriteLine(name + "  ##  " + proj.Name + "  ##  " + proj.UniqueName);
            if (proj.UniqueName == name)
            {
                return proj;
            }
            foreach (ProjectItem projItem in proj.ProjectItems)
            {
                if (projItem.SubProject != null)
                {
                    Project res = FindProject(name, projItem.SubProject);
                    if (res != null)
                    {
                        return res;
                    }
                }
            }
            return null;
        }

        public string GetVLDFilterFile(Project proj)
        {
            string folder = GetExeFolder(proj);
            do
            {
                string file = Path.Combine(folder, "vld_filter.txt");
                if (File.Exists(file))
                {
                    return file;
                }
                folder = Path.GetDirectoryName(folder);
            }
            while (folder != null && folder != String.Empty);
            return "";
        }

        public Project GetStartupSolnProject(DTE2 dte)
        {
            // Gets the name of the startup project for the solution
            // and then casts it to a Project object.
            // Have a project loaded before running this code.
            // Only one project in a solution can be a startup project.
            SolutionBuild2 sb = (SolutionBuild2)dte.Solution.SolutionBuild;
            Project startupProj = null;
            string msg = "";

            foreach (String item in (Array)sb.StartupProjects)
            {
                msg += item;
            }
            //System.Windows.Forms.MessageBox.Show("Solution startup Project: " + msg);
            foreach (Project proj in dte.Solution.Projects)
            {
                startupProj = FindProject(msg, proj);
                if (startupProj != null)
                {
                    break;
                }
            }
            //System.Windows.Forms.MessageBox.Show("Full name of solution's startup project:\n" + startupProj.FullName);
            return startupProj;
        }

        public bool IsVCProject(Project proj)
        {
            return proj.CodeModel.Language == CodeModelLanguageConstants.vsCMLanguageVC ||
                proj.CodeModel.Language == CodeModelLanguageConstants.vsCMLanguageMC;
        }

        public string GetExeFolder(Project proj)
        {
            try
            {
                VCProject vcproj = (VCProject)proj.Object;
                string configName = proj.ConfigurationManager.ActiveConfiguration.ConfigurationName;
                foreach (VCConfiguration vcConfig in vcproj.Configurations)
                {
                    if (vcConfig.ConfigurationName == configName)
                    {
                        return Path.GetDirectoryName(vcConfig.PrimaryOutput);
                    }
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            return "";
        }

        public string GetWorkingFolder(Project proj)
        {
            return Path.GetDirectoryName(proj.FullName);
        }

        public string FilterVLDOutput(string reportPath, string filterPath)
        {
            List<string> filters = new List<string>();
            if (File.Exists(filterPath))
            {
                StreamReader reader = new StreamReader(filterPath);
                string filter;
                while ((filter = reader.ReadLine()) != null)
                {
                    filter = filter.Trim();
                    if (filter != "")
                        filters.Add(filter.Trim());
                }
            }

            if (!File.Exists(reportPath))
            {
                return "";
            }

            string output = "";
            string line;
            bool inBlock = false;
            bool dropBlock = false;
            string block = "";
            string filterUsed = "";
            int blockCount = 0;
            StreamReader reportReader = new StreamReader(reportPath);
            while ((line = reportReader.ReadLine()) != null)
            {
                line += "\n";
                if (line.Contains("---------- Block "))
                {
                    block = "";
                    inBlock = true;
                    dropBlock = false;
                }
                if (inBlock)
                {
                    block += line;
                    if (!dropBlock)
                    {
                        foreach (string filter in filters)
                        {
                            if (line.Contains(filter))
                            {
                                dropBlock = true;
                                filterUsed = filter;
                            }
                        }
                    }
                    if (line == "\n")
                    {
                        inBlock = false;
                        if (dropBlock)
                        {
                            //output += "---------- Filtered out by " + filterUsed + "\n";
                        }
                        else
                        {
                            output += block;
                            ++blockCount;
                        }
                    }
                }
                else
                {
                    line = Regex.Replace(line, @"(?<=Visual Leak Detector detected )\d+(?= memory)", blockCount.ToString());
                    output += line;
                }
            }

            while (true)
            {
                string tmp = output.Replace("\n\n\n\n", "\n\n\n");
                if (tmp == output)
                {
                    break;
                }
                output = tmp;
            }
            //output += reportPath + " : original file.";
            return output;
        }

        private void OutputFullFileToPane(string leakReportPath)
        {
            OutputWindow outWin = _applicationObject.ToolWindows.OutputWindow;
            OutputWindowPane oldActive = outWin.ActivePane;

            string paneName = "Visual Leak Detector";
            OutputWindowPane pane = null;
            try
            {
                pane = outWin.OutputWindowPanes.Item(paneName);
            }
            catch
            {
                pane = outWin.OutputWindowPanes.Add(paneName);
            }

            string text = System.IO.File.ReadAllText(leakReportPath);
            pane.OutputString(text);
            oldActive.Activate();
        }

		private DTE2 _applicationObject;
		private AddIn _addInInstance;
        private OutputWindowPane outputWindowPane;
        private EnvDTE.DebuggerEvents debugEvents;
        private string leakReportPath;
        private Project startupProj;
        private bool shouldTrack;
	}
}