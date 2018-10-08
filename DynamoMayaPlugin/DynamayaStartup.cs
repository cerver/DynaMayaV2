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
using Dynamo.Interfaces;
using Dynamo.Models;
using Dynamo.ViewModels;
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
            // The executing assembly will be in Revit_20xx folder,
            // so we have to walk up one level.
            var currentAssemblyPath = Assembly.GetExecutingAssembly().Location;
            var currentAssemblyDir = Path.GetDirectoryName(currentAssemblyPath);

            var nodesDirectory = Path.Combine(currentAssemblyDir, "nodes");

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
                "GeometryColor.dll",
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
        // private static SettingsMigrationWindow migrationWindow;


        public void SetupDynamo(out DynamoViewModel viewModel)
        {

            // Temporary fix to pre-load DLLs that were also referenced in Revit folder. 
            // To do: Need to align with Revit when provided a chance.
            PreloadDynamoCoreDlls();
            var corePath = DynamoCorePath;
            var dynamoRevitExePath = Assembly.GetExecutingAssembly().Location;
            var dynamoRevitRoot = Path.GetDirectoryName(dynamoRevitExePath);// ...\Revit_xxxx\ folder

            //var umConfig = UpdateManagerConfiguration.GetSettings(new DynamoRevitLookUp());
           // var revitUpdateManager = new DynUpdateManager(umConfig);
            //revitUpdateManager.HostVersion = revitVersion; // update RevitUpdateManager with the current DynamoRevit Version
           // revitUpdateManager.HostName = "Dynamo Revit";

            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Dynamo", "Dynamo Core");
            var commonDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Dynamo", "Dynamo Core");

            var geometryFactoryPath = string.Empty;
            var preloaderLocation = string.Empty;
            PreloadShapeManager(ref geometryFactoryPath, ref preloaderLocation);



            var model = DynamoModel.Start(
                new DynamoModel.DefaultStartConfiguration
                {
                    DynamoCorePath = corePath,
                    DynamoHostPath = dynamoRevitRoot,
                    GeometryFactoryPath = geometryFactoryPath,
                    PathResolver = new PathResolver(userDataFolder, commonDataFolder),
                });

            viewModel = DynamoViewModel.Start(
                new DynamoViewModel.StartConfiguration
                {
                   // CommandFilePath = commandFilePath,
                    DynamoModel = model
                });


        }

        enum Versions { ShapeManager = 224 };
        private static void PreloadShapeManager(ref string geometryFactoryPath, ref string preloaderLocation)
        {
            var exePath = Assembly.GetExecutingAssembly().Location;
            var rootFolder = Path.GetDirectoryName(exePath);


            var versions = new[]
            {
               LibraryVersion.Version224
              
            };

            var preloader = new Preloader(rootFolder, versions);
            preloader.Preload();
            geometryFactoryPath = preloader.GeometryFactoryPath;
            preloaderLocation = preloader.PreloaderLocation;
        }

        private static void PreloadDynamoCoreDlls()
        {
            // Assume Revit Install folder as look for root. Assembly name is compromised.
            var assemblyList = new[]
            {
                "SDA\\bin\\ICSharpCode.AvalonEdit.dll"
            };

            foreach (var assembly in assemblyList)
            {
                var assemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assembly);
                if (File.Exists(assemblyPath))
                    Assembly.LoadFrom(assemblyPath);
            }
        }

        private static readonly string assemblyName = Assembly.GetExecutingAssembly().Location;
        private static ResourceManager res;
        private static string dynamopath;


        public static string DynamoCorePath
        {
            get
            {
                if (string.IsNullOrEmpty(dynamopath))
                {
                    dynamopath = GetDynamoCorePath();
                }
                return dynamopath;
            }
        }
        /// <summary>
        /// Finds the Dynamo Core path by looking into registery or potentially a config file.
        /// </summary>
        /// <returns>The root folder path of Dynamo Core.</returns>
        private static string GetDynamoCorePath()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            var dynamayaRootDir =Path.GetDirectoryName(assemblyName);
            var dynamoRoot = GetDynamoRoot(dynamayaRootDir);

            var assembly = Assembly.LoadFrom(Path.Combine(dynamayaRootDir, "DynamoInstallDetective.dll"));
            var type = assembly.GetType("DynamoInstallDetective.DynamoProducts");

            var methodToInvoke = type.GetMethod("GetDynamoPath", BindingFlags.Public | BindingFlags.Static);
            if (methodToInvoke == null)
            {
                throw new MissingMethodException("Method 'DynamoInstallDetective.DynamoProducts.GetDynamoPath' not found");
            }

            var methodParams = new object[] { version, dynamoRoot };
            return methodToInvoke.Invoke(null, methodParams) as string;
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


    }
            
        
    
}


    
