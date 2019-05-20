﻿namespace Microsoft.Content.Build.Java2Yaml
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class RunJavadoc : IStep
    {
        public RunJavadoc(string path)
        {
            RepositoryPath = path;
        }

        public string StepName => "RunJavadoc";

        public string RepositoryPath { get; }

        private static ConfigModel _config;

        public Task RunAsync(ConfigModel config)
        {
            return Task.Run(() =>
            {
                Guard.ArgumentNotNullOrEmpty(RepositoryPath, nameof(RepositoryPath));

                _config = config;

                GenerateFileList(RepositoryPath);

                var options = GenerateOptions(RepositoryPath);
                var commandLineArgs = string.Concat(options, " @files");

                ConsoleLogger.WriteLine(new LogEntry
                {
                    Phase = StepName,
                    Level = LogLevel.Info,
                    Message = $" Invoking Javadoc '{RepositoryPath}'..."
                });

                ProcessUtility.Execute("javadoc.exe", commandLineArgs, RepositoryPath);
            }
            );
        }

        private void GenerateFileList(string repositoryPath)
        {
            int count = 0;
            var inputPaths = GetInputPaths(repositoryPath).ToList();
            var targetPath = Path.Combine(repositoryPath, Constants.Files);

            using (TextWriter tw = new StreamWriter(targetPath))
            {
                foreach (var path in inputPaths)
                {
                    var files = FileUtility.GetFilesByExtension(path, Constants.Extensions.Java).ToList();

                    foreach (string file in files)
                    {
                        tw.WriteLine(file);
                        count++;
                    };
                }
            }

            ConsoleLogger.WriteLine(new LogEntry
            {
                Phase = StepName,
                Level = LogLevel.Info,
                Message = $" {count} java files to scan, detail in '{targetPath}'..."
            });
        }

        // todo: fix doclet package to allow us invoke with this format "javadoc.exe @options @files"
        private string GenerateOptions(string repositoryPath)
        {
            var option = new JavadocOptionModel();

            JavadocOptionModel.Create(option,
                 GetDependencies(RepositoryPath),
                 Path.Combine(PathUtility.GetAssemblyDirectory(), Constants.DocletLocation),
                 string.Join(";", GetInputPathsFromConfig(repositoryPath)),
                 Path.Combine(repositoryPath, Constants.Doc));

            return JavadocOptionModel.ToCmdArguments(option);
        }


        private string GetDependencies(string repositoryPath)
        {
            var packageString = new StringBuilder();
            var packages = FileUtility.GetFilesByExtension(RepositoryPath, Constants.Extensions.Jar);

            foreach (var package in packages)
            {
                packageString.Append(string.Concat(package, ";"));
            }

            return packageString.ToString();
        }

        private List<string> GetInputPaths(string repositoryPath)
        {
            var directories = GetInputPathsFromConfig(repositoryPath);

            if (_config.ExcludePaths.Count == 0)
            {
                return directories;
            }
            else
            {
                return RemoveExcludePaths(directories);
            }
        }

        private List<string> GetInputPathsFromConfig(string repositoryPath)
        {
            return (_config.InputPaths
                .Where(p => p.StartsWith(repositoryPath))
                .ToList());
        }

        private List<string> RemoveExcludePaths(List<string> directories)
        {
            var paths = new List<string>();

            foreach (var dir in directories)
            {
                var subDirectories = Directory.GetDirectories(dir, "*.*", SearchOption.AllDirectories)
                    .Where(p => !InExcludePaths(p))
                    .Select(p => p)
                    .ToList();

                paths.AddRange(subDirectories);
            }

            return paths;
        }

        private bool InExcludePaths(string dir)
        {
            var list = _config.ExcludePaths
                .Where(p => dir.StartsWith(p))
                .ToList();

            if (list.Count == 0)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }
}
