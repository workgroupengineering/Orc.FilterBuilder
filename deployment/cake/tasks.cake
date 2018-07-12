#l "generic-tasks.cake"
#l "apps-uwp-tasks.cake"
#l "apps-wpf-tasks.cake"
#l "components-tasks.cake"

#addin "nuget:?package=System.Net.Http&version=4.3.3"
#addin "nuget:?package=Newtonsoft.Json&version=11.0.2"
#addin "nuget:?package=Cake.Sonar&version=1.1.0"

#tool "nuget:?package=MSBuild.SonarQube.Runner.Tool&version=4.3.0"

var Target = GetBuildServerVariable("Target", "Default");

Information("Running target '{0}'", Target);
Information("Using output directory '{0}'", OutputRootDirectory);

//-------------------------------------------------------------

private void BuildTestProjects()
{
    foreach (var testProject in TestProjects)
    {
        Information("Building test project '{0}'", testProject);

        var projectFileName = GetProjectFileName(testProject);
        
        var msBuildSettings = new MSBuildSettings
        {
            Verbosity = Verbosity.Quiet, // Verbosity.Diagnostic
            ToolVersion = MSBuildToolVersion.VS2017,
            Configuration = ConfigurationName,
            MSBuildPlatform = MSBuildPlatform.x86, // Always require x86, see platform for actual target platform
            PlatformTarget = PlatformTarget.MSIL
        };

        // Force disable SonarQube
        msBuildSettings.WithProperty("SonarQubeExclude", "true");

        // Note: we need to set OverridableOutputPath because we need to be able to respect
        // AppendTargetFrameworkToOutputPath which isn't possible for global properties (which
        // are properties passed in using the command line)
        var outputDirectory = string.Format("{0}/{1}/", OutputRootDirectory, testProject);
        Information("Output directory: '{0}'", outputDirectory);
        msBuildSettings.WithProperty("OverridableOutputPath", outputDirectory);
        msBuildSettings.WithProperty("PackageOutputPath", OutputRootDirectory);

        MSBuild(projectFileName, msBuildSettings);
    }
}

//-------------------------------------------------------------

Task("UpdateInfo")
    .Does(() =>
{
    UpdateSolutionAssemblyInfo();
    
    UpdateInfoForComponents();
    UpdateInfoForUwpApps();
    UpdateInfoForWpfApps();
});

//-------------------------------------------------------------

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("UpdateInfo")
    .IsDependentOn("CleanupCode")
    .Does(async () =>
{
    var enableSonar = !string.IsNullOrWhiteSpace(SonarUrl);
    if (enableSonar)
    {
        SonarBegin(new SonarBeginSettings 
        {
            // SonarQube info
            Url = SonarUrl,
            Login = SonarUsername,
            Password = SonarPassword,

            // Project info
            Key = SonarProject,
            // Branch only works with the branch plugin
            //Branch = RepositoryBranchName,
            Version = VersionFullSemVer,
            
            // Minimize extreme logging
            Verbose = false,
            Silent = true,
        });
    }
    else
    {
        Information("Skipping Sonar integration since url is not specified");
    }

    BuildComponents();
    BuildUwpApps();
    BuildWpfApps();

    if (!string.IsNullOrWhiteSpace(SonarUrl))
    {
        SonarEnd(new SonarEndSettings 
        {
            Login = SonarUsername,
            Password = SonarPassword,
        });
        
        Information("Checking whether the project passed the SonarQube gateway...");
            
        var status = "none";

        // We need to use /api/qualitygates/project_status
        var client = new System.Net.Http.HttpClient();
        using (client)
        {
            var queryUri = string.Format("{0}/api/qualitygates/project_status?projectKey={1}", SonarUrl, SonarProject);

            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls;

            var byteArray = Encoding.ASCII.GetBytes(string.Format("{0}:{1}", SonarUsername, SonarPassword));
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            Debug("Invoking GET request: '{0}'", queryUri);

            var response = await client.GetAsync(new Uri(queryUri));

            Debug("Parsing request contents");

            var content = response.Content;
            var jsonContent = await content.ReadAsStringAsync();

            Debug(jsonContent);

            dynamic result = Newtonsoft.Json.Linq.JObject.Parse(jsonContent);
            status = result.projectStatus.status;
        }

        Information("SonarQube gateway status returned from request: '{0}'", status);

        if (string.IsNullOrWhiteSpace(status))
        {
            status = "none";
        }

        status = status.ToLower();

        switch (status)
        {
            case "error":
                throw new Exception(string.Format("The SonarQube gateway for '{0}' returned ERROR, please check the error(s) at {1}/dashboard?id={0}", SonarProject, SonarUrl));
                break;

            case "warn":
                Warning("The SonarQube gateway for '{0}' returned WARNING, please check the warning(s) at {1}/dashboard?id={0}", SonarProject, SonarUrl);
                break;

            case "none":
                Warning("The SonarQube gateway for '{0}' returned NONE, please check why no gateway status is available at {1}/dashboard?id={0}", SonarProject, SonarUrl);
                break;

            case "ok":
                Information("The SonarQube gateway for '{0}' returned OK, well done! If you want to show off the results, check out {1}/dashboard?id={0}", SonarProject, SonarUrl);
                break;

            default:
                throw new Exception(string.Format("Unexpected SonarQube gateway status '{0}' for project '{1}'", status, SonarProject));
                break;
        }
    }

    BuildTestProjects();
});

//-------------------------------------------------------------

Task("Package")
    // Make sure we have the temporary "project.assets.json" in case we need to package with Visual Studio
    .IsDependentOn("RestorePackages")
    // Make sure to update if we are running on a new agent so we can sign nuget packages
    .IsDependentOn("UpdateNuGet")
    .IsDependentOn("CodeSign")
    .Does(() =>
{
    PackageComponents();
    PackageUwpApps();
    PackageWpfApps();
});

//-------------------------------------------------------------
// Wrapper tasks since we don't want to add "Build" as a 
// dependency to "Package" because we want to run in multiple
// stages
//-------------------------------------------------------------

Task("BuildAndPackage")
    .IsDependentOn("Build")
    .IsDependentOn("Package");

//-------------------------------------------------------------

Task("Default")
	.IsDependentOn("BuildAndPackage");

//-------------------------------------------------------------

RunTarget(Target);