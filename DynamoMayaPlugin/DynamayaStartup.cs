using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Maya.OpenMaya;
using Dynamo.Interfaces;
using Dynamo.Models;
using Dynamo.ViewModels;
using Dynamo.Controls;
using Dynamo.ViewModels;
using DynamoInstallDetective;
using DynamoShapeManager;


namespace DynamoMaya
{
    internal class PathResolver : IPathResolver
    {
        private readonly List<string> preloadLibraryPaths;
        private readonly List<string> additionalNodeDirectories;
        private readonly List<string> additionalResolutionPaths;
        private readonly string userDataRootFolder;
        private readonly string commonDataRootFolder;

        internal PathResolver(string userDataFolder, string commonDataFolder)
        {
    
            var currentAssemblyPath = Assembly.GetExecutingAssembly().Location;
            var currentAssemblyDir = Path.GetDirectoryName(currentAssemblyPath);
            
            var progFilePath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            var nodesDirectory = Path.Combine(progFilePath, @"Dynamo\Dynamo Core\2\nodes");

            // Just making sure we are looking at the right level of nesting.
            if (!Directory.Exists(nodesDirectory))
                throw new DirectoryNotFoundException(nodesDirectory);

            preloadLibraryPaths = new List<string>
            {
                "VMDataBridge.dll",
                "ProtoGeometry.dll",
                "DesignScriptBuiltin.dll",
                "DSCoreNodes.dll",
                "DSOffice.dll",
                "DSIronPython.dll",
                "FunctionObject.ds",
                "BuiltIn.ds",
                "DynamoConversions.dll",
                "DynamoUnits.dll",
                "Tessellation.dll",
                "Analysis.dll",
                "GeometryColor.dll"
            };



            additionalNodeDirectories = new List<string> { nodesDirectory };
            additionalResolutionPaths = new List<string> { currentAssemblyDir };

            this.userDataRootFolder = userDataFolder;
            this.commonDataRootFolder = commonDataFolder;
        }

        public IEnumerable<string> AdditionalResolutionPaths
        {
            get { return additionalResolutionPaths; }
        }

        public IEnumerable<string> AdditionalNodeDirectories
        {
            get { return additionalNodeDirectories; }
        }

        public IEnumerable<string> PreloadedLibraryPaths
        {
            get { return preloadLibraryPaths; }
        }

        public string UserDataRootFolder
        {
            get { return string.Empty; }
        }

        public string CommonDataRootFolder
        {
            get { return string.Empty; }
        }
    }


    internal class DynamayaStartup
    {
        public DynamoView DynView = null;
        public DynamoViewModel DynViewModel = null;
        public DynamoModel DynModel = null;
        public bool isDynModelNull = true;
        public bool isDynViewModelNull = true;

        //public void SetupDynamo(out DynamoViewModel viewModel)
        public void SetupDynamo( )
        {
            SubscribeAssemblyResolvingEvent();
            UpdateSystemPathForProcess();

            PreloadDynamoCoreDlls();
            var corePath = DynamoCorePath;
            var dynamoMayaExeLoc = Assembly.GetExecutingAssembly().Location;
            var dynamoMayaRoot = Path.GetDirectoryName(dynamoMayaExeLoc);

  
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Dynamo", "Dynamo Core");
            var commonDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Dynamo", "Dynamo Core");

            var geometryFactoryPath = string.Empty;
            var preloaderLocation = string.Empty;
    
            var loadedLibGVersion = PreloadAsmFromMayaATF();

            try
            {
                if(isDynModelNull)
                {
                    DynModel = DynamoModel.Start(
                        new DynamoModel.DefaultStartConfiguration
                        {
                            DynamoCorePath = corePath,
                            DynamoHostPath = corePath,
                            GeometryFactoryPath = GetGeometryFactoryPath(corePath, loadedLibGVersion),
                            PathResolver = new PathResolver(userDataFolder, commonDataFolder),
                        });
                    isDynModelNull = false;
                }
                if (isDynViewModelNull)
                {
                    DynViewModel = DynamoViewModel.Start(
                    new DynamoViewModel.StartConfiguration
                    {
                    // CommandFilePath = commandFilePath,
                    DynamoModel = DynModel
                    });
                    isDynViewModelNull = false;
                }

                
            }
            catch (Exception e)
            {
                MGlobal.displayWarning(e.Message);
            }

           
        }

        //............................
        private static readonly string assemblyName = Assembly.GetExecutingAssembly().Location;
        private static ResourceManager res;
        private static string dynamopath;
        public static string DynamoCorePath
        {
            get
            {
                if (string.IsNullOrEmpty(dynamopath))
                {
                    dynamopath = GetDynamoCorePath2();
                }
                return dynamopath;
            }
        }

        //............................

        private static void PreloadShapeManager(out string geometryFactoryPath, out string preloaderLocation)
        {
            var exePath = Assembly.GetExecutingAssembly().Location;
            var rootFolder = Path.GetDirectoryName(exePath);

            var versions = new Version[]
            {
               new Version(223,0,1),
              
            };

            var preloader = new Preloader(rootFolder, versions);
            preloader.Preload();
            geometryFactoryPath = preloader.GeometryFactoryPath;
            preloaderLocation = preloader.PreloaderLocation;
        }

        internal static Version PreloadAsmFromMayaATF()
        {
            string asmLocation;
            Version libGversion = findMayaASMVersion(out asmLocation);
            
            //var dynCorePath = DynamoRevitApp.DynamoCorePath;
            var libGFolderName = string.Format("libg_{0}_{1}_{2}", libGversion.Major, libGversion.Minor, libGversion.Build);
            var preloaderLocation = Path.Combine(DynamoCorePath, libGFolderName);

            DynamoShapeManager.Utilities.PreloadAsmFromPath(preloaderLocation, asmLocation);
            return libGversion;
        }

        internal static Version findMayaASMVersion(out string asmLocation)
        {

            var mayaPath = Environment.GetEnvironmentVariable("MAYA_LOCATION");
            string asmPath = mayaPath + "/plug-ins/ATF/ATF";
            var asmFile = Directory.GetFiles(asmPath, "ASMAHL*.dll");
            if (asmFile.Length == 1)
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(asmFile[0]);
                asmLocation = asmPath;
                return new Version(versionInfo.FileMajorPart, versionInfo.FileMinorPart, versionInfo.FileBuildPart);
            }
            else
            {
                throw new Exception("Could not find Maya ASM in ATF Plugin Directory");
                
            }


        }

        public static string GetGeometryFactoryPath(string corePath, Version version)
        {
            var dynamoAsmPath = Path.Combine(corePath, "DynamoShapeManager.dll");
            var assembly = Assembly.LoadFrom(dynamoAsmPath);
            if (assembly == null)
                throw new FileNotFoundException("File not found", dynamoAsmPath);

            var utilities = assembly.GetType("DynamoShapeManager.Utilities");
            var getGeometryFactoryPath = utilities.GetMethod("GetGeometryFactoryPath2");

            return (getGeometryFactoryPath.Invoke(null,
                new object[] { corePath, version }) as string);
        }

        private static void PreloadDynamoCoreDlls()
        {
          
            var assemblyList = new[]
            {
                "C:\\Program Files\\Dynamo\\Dynamo Core\\2\\DynamoCoreWpf.dll",
            };

            foreach (var assembly in assemblyList)
            {
                var assemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assembly);
                if (File.Exists(assemblyPath))
                    Assembly.LoadFrom(assemblyPath);
            }
        }

        private static string GetDynamoRoot(string dynamoCoreRoot)
        {
            //TODO: use config file to setup Dynamo Path for debug builds.

            //When there is no config file, just replace DynamoRevit by Dynamo 
            //from the 'dynamoRevitRoot' folder.
            var parent = new DirectoryInfo(dynamoCoreRoot);
            var path = string.Empty;
            while (null != parent && parent.Name != @"DynamoCore")
            {
                path = Path.Combine(parent.Name, path);
                parent = Directory.GetParent(parent.FullName);
            }

            return parent != null ? Path.Combine(Path.GetDirectoryName(parent.FullName), @"Dynamo", path) : dynamoCoreRoot;
        }

        private static string GetDynamoCorePath2()
        {
            var progFilePath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            var dynamoCorePath =  Path.Combine(progFilePath, @"Dynamo\Dynamo Core\2\");
            if(Directory.Exists(dynamoCorePath))
            {
                return dynamoCorePath;
            }else
            {
                throw new Exception($"Cannot find Dynamo core directory at --{dynamoCorePath}--");
            }

        }

        public static void UpdateSystemPathForProcess()
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

        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            var assemblyPath = string.Empty;
            var assemblyName = new AssemblyName(args.Name).Name + ".dll";

            try
            {
                // var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                var dynCorePath = @"C:\Program Files\Dynamo\Dynamo Core\2";
                var dynNodePath = @"C:\Program Files\Dynamo\Dynamo Core\2\nodes";
                var dynLocalUS = @"C:\Program Files\Dynamo\Dynamo Core\2\en-US";
                var dynUIloc = @"C:\Program Files\Dynamo\Dynamo Core\2\UI\Themes\Modern";

                assemblyPath = Path.Combine(dynCorePath, assemblyName);
                if (!File.Exists(assemblyPath))
                {
                    // If assembly cannot be found, try in nodes
                    assemblyPath = Path.Combine(dynNodePath, assemblyName);
                    if (!File.Exists(assemblyPath))
                    {
                        assemblyPath = Path.Combine(dynLocalUS, assemblyName);
                        if (!File.Exists(assemblyPath))
                        {
                            assemblyPath = Path.Combine(dynUIloc, assemblyName);
                        }
                    }
                }

                return (File.Exists(assemblyPath) ? Assembly.LoadFrom(assemblyPath) : null);
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"The location of the assembly, {assemblyPath} could not be resolved for loading.",
                    ex);
            }
        }

        public bool InitializeCoreView()
        {
            if (DynViewModel == null) return false;
            var mwHandle = Process.GetCurrentProcess().MainWindowHandle;
            var dynamoView = new DynamoView(DynViewModel);
            new WindowInteropHelper(dynamoView).Owner = mwHandle;
            
            DynView =  dynamoView;
            return true;
        }

    }
            
        
    
}


    
