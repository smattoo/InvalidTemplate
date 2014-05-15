<Query Kind="Program">
  <Connection>
    <ID>84a05001-6daa-45cb-a099-2b6cda22c67c</ID>
    <Persist>true</Persist>
    <Server>(localdb)\v11.0</Server>
    <Database>schedeventstore</Database>
    <ShowServer>true</ShowServer>
  </Connection>
  <Reference Relative="..\..\BrewMaster\brewmaster\src\Worker\bin\Debug\Brewmaster.TemplateSDK.Contracts.dll">E:\Git_Local\BrewMaster\brewmaster\src\Worker\bin\Debug\Brewmaster.TemplateSDK.Contracts.dll</Reference>
  <Reference Relative="..\..\BrewMaster\brewmaster\src\Worker\bin\Debug\Brewmaster.TemplateSDK.dll">E:\Git_Local\BrewMaster\brewmaster\src\Worker\bin\Debug\Brewmaster.TemplateSDK.dll</Reference>
  <Namespace>Brewmaster.TemplateSDK.Contracts</Namespace>
  <Namespace>Brewmaster.TemplateSDK.Contracts.Fluent</Namespace>
  <Namespace>Brewmaster.TemplateSDK.Contracts.Models</Namespace>
  <Namespace>Brewmaster.TemplateSDK.Contracts.Serialization</Namespace>
  <Namespace>Brewmaster.TemplateSDK.Contracts.Validation</Namespace>
</Query>

void Main()
{
	var template = WithTemplateExtensions
                .CreateTemplate("Brewmaster.ArrReverseProxy", "ArrReverseProxy")
                .WithAffinityGroup("{{AffinityGroup}}", "{{Region}}")
				.WithStorageAccount("{{DiskStore}}")
				.WithCloudService("{{CloudService}}","Brewmaster ARR Cloud Service",
					cs=>cs.WithDeployment(null,d=>
					d.UsingDefaultDiskStorage("{{DiskStore}}")
						.WithVirtualMachine("{{ServerNamePrefix}}","{{VmSize}}","arr-avset",vm=>
											vm.WithWindowsConfigSet("vmadmin")
											.UsingConfigSet("ArrServer")))
				)
				.WithCredential("vmadmin","{{AdminName}}","{{AdminPassword}}")
				.WithConfigSet("ArrServer", "ARR Server",
                          r =>
                          r.WithEndpoint("HTTP", 80, 80,
                                         new EndpointLoadBalancerProbe
                                             {
                                                 Name = "http",
                                                 Protocol = "Http",
                                                 Path = "/",
                                                 IntervalInSeconds = 15,
                                                 TimeoutInSeconds = 31
                                             })
                           .WithEndpoint("HTTPS", 443, 443,
                                         new EndpointLoadBalancerProbe
                                             {
                                                 Name = "https",
                                                 Protocol = "Tcp",
                                                 IntervalInSeconds = 15,
                                                 TimeoutInSeconds = 31
                                             })
                           .UsingConfiguration("InstallArr"));
				
	template.Configurations = new[]
                {
                    new Brewmaster.TemplateSDK.Contracts.Models.Configuration
                        {
                            Name = "InstallArr",
                            Resources = new []
                                {
                                    new GenericResource("Package")
                                        {
                                            Name = "InstallWebPI",
                                            Args = new Dictionary<string, string>
                                                {
                                                    {"Credential", "vmadmin"},
                                                    {"Name", "Microsoft Web Platform Installer 4.6"},
                                                    {"ProductId", "16C7D2AD-20CA-491E-80BC-8607A9AACED9"},
                                                    {
                                                        "Path",
                                                        @"http://download.microsoft.com/download/7/0/4/704CEB4C-9F42-4962-A2B0-5C84B0682C7A/WebPlatformInstaller_amd64_en-US.msi"
                                                    },
                                                    {
                                                        "LogPath",
                                                        @"%BrewmasterDir%\Logs\WebPlatformInstaller_amd64_en-US.log"
                                                    },
                                                    {"Ensure", "Present"},
                                                },
                                        },
                                    new GenericResource("WindowsFeature")
                                        {
                                            Name = "InstallASPNET45",
                                            Args = new Dictionary<string, string>
                                                {
                                                    {"Name", "NET-Framework-45-ASPNET"},
                                                    {"IncludeAllSubFeature", "true"},
                                                    {"LogPath", @"%BrewmasterDir%\Logs\Install-ASPNET45.log"},
                                                    {"Ensure", "Present"},
                                                },
                                        },
                                    new GenericResource("WindowsFeature")
                                        {
                                            Name = "InstallIIS",
                                            Args = new Dictionary<string, string>
                                                {
                                                    {"Name", "Web-Server"},
                                                    {"IncludeAllSubFeature", "true"},
                                                    {"LogPath", "%BrewmasterDir%\\Logs\\Install-IIS.log"},
                                                    {"Ensure", "Present"},
                                                },
                                        },
                                    new GenericResource("Registry")
                                        {
                                            Name = "EnableRemoteManagement",
                                            Args = new Dictionary<string, string>
                                                {
                                                    {
                                                        "Key",
                                                        @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WebManagement\Server"
                                                    },
                                                    {"ValueName", "EnableRemoteManagement"},
                                                    {"ValueData", "1"},
                                                    {"ValueType", "Dword"},
                                                    {"Force", "true"},
                                                    {"Ensure", "Present"},
                                                },
                                            Requires = new[] {"[WindowsFeature]InstallIIS", "[WindowsFeature]InstallASPNET45"}
                                        },
                                    new GenericResource("Service")
                                        {
                                            Name = "StartWebManagementService",
                                            Args = new Dictionary<string, string>
                                                {
                                                    {"Name", "wmsvc"},
                                                    {"StartupType", "Automatic"},
                                                    {"State", "Running"},
                                                },
                                            Requires = new[] {"[Registry]EnableRemoteManagement"},
                                        },
									new ScriptResource
                                        {
                                            Name = "InstallARR",
                                            Credential = "vmadmin",
											TestScript =
													@"if (Test-Path -LiteralPath ""$env:ProgramFiles\IIS\Application Request Routing\requestRouter.dll"" -PathType Leaf)
{Write-Verbose ""$env:ProgramFiles\IIS\Application Request Routing\requestRouter.dll already exists."" -Verbose
return $true}
return $false",
											SetScript =
													@"$webpicmdexe = ""$env:ProgramFiles\Microsoft\Web Platform Installer\WebPICmd.exe""
$webpicmdargs = @(""/Install"", ""/Products:ARR"", ""/AcceptEula"", ""/SuppressReboot"", ""/Log:$env:BrewmasterDir\Logs\ARR-Install.log"")
Write-Verbose ""Installing ARR ($webpicmdexe $webpicmdargs)"" -Verbose
Start-Process -FilePath $webpicmdexe -ArgumentList $webpicmdargs -Wait"
											,
											GetScript =
													@"return @{ Installed = Test-Path -LiteralPath ""$env:ProgramFiles\IIS\Application Request Routing\requestRouter.dll"" -PathType Leaf }",
											Requires=new []{"[WindowsFeature]InstallIIS", "[WindowsFeature]InstallASPNET45"},
										},
									new ScriptResource
                                        {
                                            Name = "EnableARR",
                                            Credential = "vmadmin",
											TestScript =
													@"if ((Get-WebConfigurationProperty -Filter //proxy -Name enabled).Value)
{Write-Verbose ""ApplicationRequestRouting proxy already enabled"" -Verbose
return $true}
return $false",
											SetScript =
													@"$appcmdexe = ""$env:SystemRoot\system32\inetsrv\appcmd""
$appcmdargs = 'set config -section:system.webServer/proxy /enabled:""True"" /commit:apphost'
Write-Verbose ""Enabling ARR ($appcmdexe $appcmdargs)"" -Verbose
Start-Process -FilePath $appcmdexe -ArgumentList $appcmdargs -Wait"
											,
											GetScript = @"return @{ Enabled = (Get-WebConfigurationProperty -Filter //proxy -Name enabled).Value}",
											Requires=new []{"[WindowsFeature]InstallIIS", "[WindowsFeature]InstallASPNET45"},
										}
                                }
                        }
                };
				
  	template = template.WithParameter("Region", ParameterType.String, "Name of Azure region.", "AzureRegionName")
                .WithParameter("AffinityGroup", ParameterType.String, "Name of Azure affinity group.",
                               "AzureAffinityGroupName")
                .WithParameter("CloudService", ParameterType.String, "Name of the Azure Cloud Service.",
                               "AzureCloudServiceName")
                .WithParameter("DiskStore", ParameterType.String, "Name of Azure disk storage account.",
                               "AzureStorageName")
                .WithParameter("VMSize", ParameterType.String, "Size of the server VMs.", "AzureRoleSize",
                               p => p.WithDefaultValue("Small"))
                .WithParameter("AdminName", ParameterType.String, "Name of local administrator account.", "username",
                               p => p.WithLimits(1, 64))
                .WithParameter("AdminPassword", ParameterType.String, "Password of local administrator account.",
                               "password",
                               p => p.WithLimits(8, 127), maskValue: true)
                .WithParameter("ServerNamePrefix", ParameterType.String, "Name prefix for web servers.",
                               p => p.WithDefaultValue("arr")
                                     .WithRegexValidation(@"^[a-zA-Z][a-zA-Z0-9-]{1,13}$",
                                                          "Must contain 3 to 14 letters, numbers, and hyphens. Must start with a letter."))
                .WithParameter("NumberOfWebServers", ParameterType.Number, "Number of web servers.", "integer",
                               p => p.WithDefaultValue("2")
                                     .WithLimits(2, 100)
                                     .WithRegexValidation(@"^\d+$", "Must enter a positive integer between 2 and 100."));
	template.Save(@"E:\WorkFiles\ARR");
}