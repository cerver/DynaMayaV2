using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Syndication;
using System.Windows;
using System.Windows.Interop;
using Autodesk.Maya;
using Autodesk.Maya.OpenMaya;
using Autodesk.Maya.OpenMayaUI;
using Dynamo.Controls;
using Dynamo.ViewModels;
using DynamoMaya;
using System.Windows.Media;
using Autodesk.Maya.OpenMayaRender.MHWRender;
using Dynamo.Wpf.Interfaces;

[assembly: ExtensionPlugin(typeof(StartDynamayaPlugin), "Any")]
[assembly: MPxCommandClass(typeof (DynaMayaCommand), "dynaMaya")]

namespace DynamoMaya
{


    public class DynaMayaCommand : MPxCommand, IMPxCommand
    { 
        static readonly string flagName = "nodock";
        static readonly string pluginName = "DynaMaya";
        static readonly string commandName = "DynaMaya";
 
        //DynamoViewModel viewModel;
        //DynamoView dynamoViewWnd;
        private DynamayaStartup dynStartUp; // WPF window  
        private DynamoView dynWnd;
        static MForeignWindowWrapper mayaWnd;    // Maya's host window for this WPF window
        static string wpfTitle;
        static string hostTitle;

        public override void doIt(MArgList argl)
        {
            SubscribeAssemblyResolvingEvent();
            

            if (!String.IsNullOrEmpty(wpfTitle))
            {
                // Check the existence of the window
                int wndExist = int.Parse(MGlobal.executeCommandStringResult($@"format -stringArg `control -q -ex ""{wpfTitle}""` ""^1s"""));
                if (wndExist > 0)
                {
                    MGlobal.executeCommand($@"catch (`workspaceControl -e -visible true ""{hostTitle}""`);");
                    return;
                }
            }

            if (dynStartUp != null)
            {
                if (dynStartUp.DynView != null)
                {
                    if (dynStartUp.DynView.IsVisible)
                    {
                        MGlobal.displayWarning("Dynamo is already open");
                        return;
                    }
                    else
                    {
                        dynWnd.Show();
                    }
                }
            }
            else
            {
                newDmStartup();
                dynWnd = dynStartUp.DynView;
                // Create the window to dock
                dynWnd.Show();
                // Extract the window handle of the window we want to dock
                IntPtr mWindowHandle = new System.Windows.Interop.WindowInteropHelper(dynWnd).Handle;

                int width = (int)dynWnd.Width;
                int height = (int)dynWnd.Height;

                var title = dynWnd.Title;
                wpfTitle = title + " Internal";
                hostTitle = title;

                dynWnd.Title = wpfTitle;

                mayaWnd = new MForeignWindowWrapper(mWindowHandle, true);

              
                uint flagIdx = argl.flagIndex(flagName);
                if (flagIdx == MArgList.kInvalidArgIndex)
                {
                    // Create a workspace-control to wrap the native window wrapper, and use it as the parent of this WPF window
                    CreateWorkspaceControl(wpfTitle, hostTitle, width, height, false);
                }
                
                
                
                
            }

        }

      
        private DynamayaStartup newDmStartup()
        {
            dynStartUp = new DynamayaStartup();
            dynStartUp.SetupDynamo();
            dynStartUp.InitializeCoreView();
            return dynStartUp;
        }

        private void View_Closed(object sender, EventArgs e)
        {
            //DynamoView dv = (DynamoView)sender;
            if (!dynStartUp.DynViewModel.PerformShutdownSequence(new DynamoViewModel.ShutdownParams(false, true, true)))
            {
                MGlobal.displayWarning("Could not shut down");
            }
          
            dynStartUp.DynView.Close();
            dynStartUp = null;

        }

        private static void CreateWorkspaceControl(string content, string hostName, int width, int height, bool retain = true, bool floating = true)
        {
            string closeCommand =  $"workspaceControl -cl {hostName};";

            string command = $@"
                    workspaceControl 
                        -requiredPlugin DynaMaya
                        -cp true
                        -retain {retain.ToString().ToLower()} 
                        -floating {floating.ToString().ToLower()}
                        -uiScript ""if (!`control -q -ex \""{content}\""`) {commandName} -{flagName}; control -e -parent \""{hostName}\"" \""{content}\"";""
                        -requiredPlugin {pluginName}
                        -initialWidth {width}
                        -initialHeight {height} 
                        ""{hostName}"";
                ";
            try
            {
                MGlobal.executeCommand(command);
            }
            catch (Exception)
            {
                Console.WriteLine("Error while creating workspace-control.");
            }
        }

        public void SubscribeAssemblyResolvingEvent()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
        }

        public static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            var assemblyPath = string.Empty;
            var assemblyName = new AssemblyName(args.Name).Name + ".dll";

            try
            {
               // var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                var dynCorePath = @"C:\Program Files\Dynamo\Dynamo Core\2";
                var dynNodePath = @"C:\Program Files\Dynamo\Dynamo Core\2\nodes";

                assemblyPath = Path.Combine(dynCorePath, assemblyName);
                if (!File.Exists(assemblyPath))
                {
                    // If assembly cannot be found, try in nodes
                    assemblyPath = Path.Combine(dynNodePath, assemblyName);
                }

                return (File.Exists(assemblyPath) ? Assembly.LoadFrom(assemblyPath) : null);
            }
            catch (Exception ex)
            {
                throw new Exception(
                    string.Format("The location of the assembly, {0} could not be resolved for loading.", assemblyPath),
                    ex);
            }
        }

        
    }

    public class StartDynamayaPlugin : IExtensionPlugin
    {
        bool IExtensionPlugin.InitializePlugin()
        {
            return true;
        }

        bool IExtensionPlugin.UninitializePlugin()
        {
            return true;
        }

        string IExtensionPlugin.GetMayaDotNetSdkBuildVersion()
        {
            String version = "201853";
            return version;
        }

     
    }

}