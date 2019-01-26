﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Versatile;

namespace DevAudit.AuditLibrary
{
    public class NetCorePackageSource : PackageSource
    {
        public override string PackageManagerId { get { return "netcore"; } }

        public override string PackageManagerLabel { get { return ".NET Core"; } }

        public override string DefaultPackageManagerConfigurationFile { get { return string.Empty; } }

        public NetCorePackageSource(Dictionary<string, object> package_source_options, 
            EventHandler<EnvironmentEventArgs> message_handler = null) : base(package_source_options, message_handler)
        {}

        public override IEnumerable<Package> GetPackages(params string[] o)
        {
            AuditFileInfo config_file = this.AuditEnvironment.ConstructFile(this.PackageManagerConfigurationFile);
            List<Package> packages = new List<Package>();
            if (config_file.Name.EndsWith(".csproj"))
            {
                try
                {
                    this.AuditEnvironment.Info("Reading packages from .NET Core C# .csproj file.");
                    string _byteOrderMarkUtf8 = Encoding.UTF8.GetString(Encoding.UTF8.GetPreamble());
                    string xml = config_file.ReadAsText();
                    if (xml.StartsWith(_byteOrderMarkUtf8, StringComparison.Ordinal))
                    {
                        var lastIndexOfUtf8 = _byteOrderMarkUtf8.Length;
                        xml = xml.Remove(0, lastIndexOfUtf8);
                    }
                    XElement root = XElement.Parse(xml);


                    if (root.Name == "Project")
                    {
                        packages = root.Descendants().Where(x => x.Name == "PackageReference").Select(r =>
                            new Package("nuget", r.Attribute("Include").Value, r.Attribute("Version").Value)).ToList();
                        return packages;
                    }
                    else
                    {
                        this.AuditEnvironment.Error("{0} is not a .NET Core format .csproj file.", config_file.FullName);
                        return packages;
                    }

                    
                }
                catch (XmlException e)
                {
                    throw new Exception("XML exception thrown parsing file: " + this.PackageManagerConfigurationFile, e);
                }
                catch (Exception e)
                {
                    throw new Exception("Unknown exception thrown attempting to get packages from file: "
                        + this.PackageManagerConfigurationFile, e);
                }
            }
            else if (config_file.Name.EndsWith(".deps.json"))
            {
                try
                {
                    this.AuditEnvironment.Info("Reading packages from .NET Core depedencies manifest..");
                    JObject json = (JObject)JToken.Parse(config_file.ReadAsText());
                    JObject libraries = (JObject)json["dli"];

                    if (libraries != null)
                    {
                        foreach (JProperty p in libraries.Properties())
                        {
    
                            string[] name = p.Name.Split('/');
                            packages.Add(new Package("nuget", name[0], name[1]));
                        }
                    }
                    return packages;
                }
                catch (Exception e)
                {
                    this.AuditEnvironment.Error(e, "Error reading .NET Core dependencies manifest {0}.", config_file.FullName);
                    return packages;
                }
            }
            else
            {
                this.AuditEnvironment.Error("Unknown .NET Core prooject file type: {0}.", config_file.FullName);
                return packages;
            }

        }

        public override bool IsVulnerabilityVersionInPackageVersionRange(string vulnerability_version, string package_version)
        {
            string message = "";
            bool r = NuGetv2.RangeIntersect(vulnerability_version, package_version, out message);
            if (!r && !string.IsNullOrEmpty(message))
            {
                throw new Exception(message);
            }
            else return r;
        }
    }
}
