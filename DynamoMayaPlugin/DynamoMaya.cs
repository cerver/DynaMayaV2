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

//[assembly: MPxCommandClass(typeof (StartDynamaya), "dynaMaya")]

[assembly: MPxCommandClass(typeof (StatDynamaya), "dynaMaya")]
[assembly: ExtensionPlugin(typeof (StatDynamaya), "CERVER Design Studio and Autodesk Dynamo")]

namespace DynamoMaya
{

    public class StatDynamaya : MPxCommand, IMPxCommand
    {
        static readonly string flagName = "nodock";
        static readonly string pluginName = "DynaMaya";
        static readonly string commandName = "DynaMaya";

        //private MForeignWindowWrapper mfww;
        // Objects to keep around
        static DynamoView view;   
        static MForeignWindowWrapper mayaWnd;    
        static string wpfTitle;
        static string hostTitle;

        private DynamoViewModel viewModel = null;

        // internal RemoteConnection remoteCon;
        public override void doIt(MArgList argl)
        {
            //ToDo: fix embeded wpf dockble window
            MGlobal.displayInfo("DynaMaya is starting........");

       

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

            if (view != null)
            {
                if (view.IsVisible)
                {
                    MGlobal.displayWarning("Dynamo is already open");
                    return;
                }
            }

            RenderOptions.ProcessRenderMode = RenderMode.Default;
            

            SubscribeAssemblyResolvingEvent();
            UpdateSystemPathForProcess();
            
        
            DynamayaStartup dmStartup = new DynamayaStartup();
            dmStartup.SetupDynamo(out viewModel);

            // show the window
            
            view = InitializeCoreView(viewModel);
            view.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            view.Show();

            // Extract the window handle of the window we want to dock
            IntPtr mWindowHandle = new WindowInteropHelper(view).Handle;
            view.Closed += View_Closed;

            

            int width = (int)view.Width;
            int height = (int)view.Height;

            var title = view.Title;
            wpfTitle = title + " Internal";
            hostTitle = title;

            view.Title = wpfTitle;
           
            
            mayaWnd = new MForeignWindowWrapper(mWindowHandle, true);
            

            uint flagIdx = argl.flagIndex(flagName);
            if (flagIdx == MArgList.kInvalidArgIndex)
            {
                // Create a workspace-control to wrap the native window wrapper, and use it as the parent of this WPF window
                CreateWorkspaceControl(wpfTitle, hostTitle, width, height);
            }

           

            MGlobal.displayInfo("DynaMaya: Dynamo for Maya |  Copyright© 2018 CERVER Design Studio & Robert Cervellione | http://www.cerver.io");
            MGlobal.displayInfo("For more information on Dynamo please visit http://www.dynamobim.com");

        }

        private void View_Closed(object sender, EventArgs e)
        {
            DynamoView dv = (DynamoView)sender;
            if (!viewModel.PerformShutdownSequence(new DynamoViewModel.ShutdownParams(false, true, true)))
            {
                MGlobal.displayWarning("Could not shut down");
            }
            MGlobal.executeCommand($@"catch (`workspaceControl -cl ""{hostTitle}""`);");
            view.Dispose();
            //dv.Close();
            //dv = null;

        }

        private static void UpdateSystemPathForProcess()
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
            var parentDirectory = Directory.GetParent(assemblyDirectory);
            var corePath = assemblyDirectory;


            var path =
                    Environment.GetEnvironmentVariable(
                        "Path",
                        EnvironmentVariableTarget.Process) + ";" + corePath;
            Environment.SetEnvironmentVariable("Path", path, EnvironmentVariableTarget.Process);
        }

        private void SubscribeAssemblyResolvingEvent()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
        }

        public static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            var assemblyPath = string.Empty;
            var assemblyName = new AssemblyName(args.Name).Name + ".dll";

            try
            {
                var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);

                // Try "Dynamo 0.x\Revit_20xx" folder first...
                assemblyPath = Path.Combine(assemblyDirectory, assemblyName);
                if (!File.Exists(assemblyPath))
                {
                    // If assembly cannot be found, try in "Dynamo 0.x" folder.
                    var parentDirectory = Directory.GetParent(assemblyDirectory);
                    assemblyPath = Path.Combine(parentDirectory.FullName, assemblyName);
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

        private static DynamoView InitializeCoreView(DynamoViewModel dynamoViewModel)
        {
            var mwHandle = Process.GetCurrentProcess().MainWindowHandle;
            var dynamoView = new DynamoView(dynamoViewModel);
            new WindowInteropHelper(dynamoView).Owner = mwHandle;

            return dynamoView;
        }

  

        private static void CreateWorkspaceControl(string content, string hostName, int width, int height, bool retain = true, bool floating = true)
        {
            String command = $@"
                    workspaceControl 
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
            String version = "201353";
            return version;
        }
    }

}