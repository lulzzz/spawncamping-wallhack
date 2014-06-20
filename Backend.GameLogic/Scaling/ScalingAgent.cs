﻿namespace Backend.GameLogic.Scaling
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using Microsoft.WindowsAzure;
    using Microsoft.WindowsAzure.Management;
    using Microsoft.WindowsAzure.Management.Compute;
    using Microsoft.WindowsAzure.Management.Compute.Models;
    using Microsoft.WindowsAzure.Management.Scheduler;
    using Microsoft.WindowsAzure.Management.Storage;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;
    using System.Text;
    using Newtonsoft.Json;
using Microsoft.WindowsAzure.Management.Storage.Models;
    using Microsoft.WindowsAzure.Storage.Auth;

    public class ScalingAgent
    {
        private string subscriptionID;
        private string subscriptionManagementCertificateThumbprint;

        private ComputeManagementClient computeManagementClient;
        private StorageManagementClient storageManagementClient;
        private CloudServiceManagementClient cloudServiceManagementClient;
        private ManagementClient managementClient;


        public ScalingAgent(string subscriptionID, string subscriptionManagementCertificateThumbprint)
        {
            this.subscriptionID = subscriptionID;
            this.subscriptionManagementCertificateThumbprint = subscriptionManagementCertificateThumbprint;

            X509Certificate2 managementCert = this.subscriptionManagementCertificateThumbprint.FindX509CertificateByThumbprint();
            SubscriptionCloudCredentials creds = new CertificateCloudCredentials(this.subscriptionID, managementCert);

            this.computeManagementClient = CloudContext.Clients.CreateComputeManagementClient(creds);
            this.storageManagementClient = CloudContext.Clients.CreateStorageManagementClient(creds);
            this.cloudServiceManagementClient = CloudContext.Clients.CreateCloudServiceManagementClient(creds);
            this.managementClient = CloudContext.Clients.CreateManagementClient(creds);
        }

        public async Task ScaleAsync()
        {
            //foreach (var s in (await this.computeManagementClient.HostedServices.ListAsync()))
            //{
            //    Console.WriteLine(s.ServiceName);
            //}

            //foreach (var s in (await this.managementClient.RoleSizes.ListAsync()).Where(_ => _.SupportedByVirtualMachines).OrderBy(_ => _.Cores))
            //{
            //    Console.WriteLine("Name {0}", s.Name);
            //}

            //foreach (var s in await managementClient.Locations.ListAsync())
            //{
            //    Console.WriteLine("Location name {0} dieplay \"{1}\"", s.Name, s.DisplayName);
            //}

            //foreach (var s in await storageManagementClient.StorageAccounts.ListAsync())
            //{
            //    Console.WriteLine("Storage  {0} region {1}", s.Name, s.Properties.GeoPrimaryRegion);
            //}


            var imageLabel = "Windows Server 2012 R2 Datacenter, May 2014";

            var vmname = string.Format("cgp{0}", System.DateTime.UtcNow.ToString("yyMMddhhmmss"));
            var imageName = (await computeManagementClient.VirtualMachineOSImages.ListAsync())
                .Where(_ => _.Category == "Public" && _.Label == imageLabel)
                .First().Name;

            var instanceSize = "Basic_A0";
            var datacenter = "West Europe";
            var adminuser = "chgeuer";
            var externalRdpPort = 54523;
            var adminpass = Environment.GetEnvironmentVariable("AzureVmAdminPassword");

            var storageAccount = (await storageManagementClient.StorageAccounts.ListAsync()).Where(_ => _.Properties.Location == datacenter).First();

            var osDiskMediaLocation = string.Format("https://{0}.blob.core.windows.net/vhds/{1}-OSDisk.vhd",
                storageAccount.Name, vmname);

            var role = new Role
            {
                RoleName = vmname,
                Label = vmname,
                RoleSize = instanceSize,
                VMImageName = imageName,
                ProvisionGuestAgent = true,
                RoleType = "PersistentVMRole",
                OSVirtualHardDisk = new OSVirtualHardDisk
                {
                    HostCaching = "ReadWrite",
                    MediaLink = new Uri(osDiskMediaLocation)
                }
            }
                .AddWindowsProvisioningConfiguration(
                    computerName: vmname,
                    adminUserName: adminuser,
                    adminPassword: adminpass,
                    resetPasswordOnFirstLogon: false,
                    enableAutomaticUpdates: true)
                .AddInputEndpoint(new InputEndpoint
                    {
                        Name = "RDP",
                        EnableDirectServerReturn = false,
                        Protocol = "tcp",
                        Port = externalRdpPort,
                        LocalPort = 3389
                    })
                .AddBGInfoExtension()
                .AddCustomScriptExtension(
                    storageAccount: await storageManagementClient.ToCloudStorageAccountAsync(storageAccount), 
                    containerName: "scripts", 
                    filename: "cgp140620110755.ps1", 
                    arguments: "c:\\hello_from_customscriptextension");

            await computeManagementClient.VirtualMachines.CreateDeploymentAsync(
                serviceName: vmname,
                parameters: new VirtualMachineCreateDeploymentParameters
                {
                    Name = vmname,
                    Label = vmname,
                    DeploymentSlot = DeploymentSlot.Production,
                    Roles = new List<Role> { role }
                });
        }

        private static Uri GetVhdUri(string blobcontainerAddress,
            string cloudServiceName, string vmName, bool cacheDisk = false, bool https = false)
        {
            var now = DateTime.UtcNow;
            string dateString = now.Year + "-" + now.Month + "-" + now.Day;

            var address = string.Format("http{0}://{1}/{2}-{3}-{4}-{5}-650.vhd", https ? "s" :
                string.Empty, blobcontainerAddress, cloudServiceName, vmName, cacheDisk ? "-CacheDisk" : string.Empty, dateString);
            return new Uri(address);
        }

        public static void CreateVMDeployment(IVirtualMachineOperations operations, string cloudServiceName, string deploymentName, List<Role> roleList, DeploymentSlot slot = DeploymentSlot.Production)
        {
            try
            {
                VirtualMachineCreateDeploymentParameters createDeploymentParams = new VirtualMachineCreateDeploymentParameters
                {
                    Name = deploymentName,
                    Label = cloudServiceName,
                    Roles = roleList,
                    DeploymentSlot = slot
                };
                operations.CreateDeployment(cloudServiceName, createDeploymentParams);
            }
            catch (CloudException e)
            {
                throw e;
            }
        }

        //public async Task ScaleAsync()
        //{
        //    var detailed = await computeManagementClient.HostedServices.GetDetailedAsync(serviceName);
        //    var deployment = detailed.Deployments.First(_ => _.DeploymentSlot == DeploymentSlot.Production);
        //    var doc = XDocument.Parse(deployment.Configuration);
        //
        //    setInstanceCount(doc, Names.GameRole.Name, 5);
        //
        //    var operationResponse = await computeManagementClient.Deployments.BeginChangingConfigurationBySlotAsync(
        //            serviceName: detailed.ServiceName,
        //            deploymentSlot: deployment.DeploymentSlot,
        //            cancellationToken: CancellationToken.None,
        //            parameters: new DeploymentChangeConfigurationParameters()
        //            {
        //                Configuration = doc.ToString()
        //            });
        //
        //    if (operationResponse.StatusCode != System.Net.HttpStatusCode.OK)
        //    {
        //        Trace.TraceError(string.Format(
        //                    "Problem scaling: HTTPStatus: {0} RequestID {1}",
        //                    operationResponse.StatusCode.ToString(),
        //                    operationResponse.RequestId));
        //    }
        //
        //    Trace.TraceInformation(string.Format(
        //                "Scaling done: HTTPStatus: {0} RequestID {1}",
        //                operationResponse.StatusCode.ToString(),
        //                operationResponse.RequestId));
        //}
        //
        //private static XName n(string name)
        //{
        //    return XName.Get(name,
        //        "http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceConfiguration");
        //}
        //
        //private static int getInstanceCount(XDocument doc, string rolename)
        //{
        //    var role = doc.Root.Elements(n("Role")).FirstOrDefault(_ => _.Attribute("name").Value == rolename);
        //    if (role == null) return -1;
        //    var v = role.Element(n("Instances")).Attribute("count").Value;
        //    return int.Parse(v);
        //}
        //
        //private static void setInstanceCount(XDocument doc, string rolename, int newInstanceCount)
        //{
        //    if (newInstanceCount < 1)
        //    {
        //        newInstanceCount = 1;
        //    }

        //    var role = doc.Root.Elements(n("Role")).FirstOrDefault(_ => _.Attribute("name").Value == rolename);
        //    role.Element(n("Instances")).Attribute("count").Value = newInstanceCount.ToString();
        //}

        //private static void changeInstanceCount(XDocument doc, string rolename, int deltaCount)
        //{
        //    int oldCount = getInstanceCount(doc, rolename);
        //    var newCount = oldCount + deltaCount;
        //    setInstanceCount(doc, rolename, newCount);
        //}
    }


    public static class ScalingExtensions
    {
        private static ConfigurationSet GetConfigurationSet(this Role role, string configurationSetType) 
        {
            if (role.ConfigurationSets == null)
            {
                role.ConfigurationSets = new List<ConfigurationSet>();

            }
            var configurationSet = role.ConfigurationSets.FirstOrDefault(_ => _.ConfigurationSetType == configurationSetType);
            if (configurationSet == null)
            {
                configurationSet = new ConfigurationSet { ConfigurationSetType = configurationSetType };
                role.ConfigurationSets.Add(configurationSet);
            }

            return configurationSet;
        }

        public static Role AddWindowsProvisioningConfiguration(this Role role,
            string computerName, string adminUserName, string adminPassword,
            bool resetPasswordOnFirstLogon = false, bool enableAutomaticUpdates = true, string timeZone = "")
        {
            var configurationSet = role.GetConfigurationSet("WindowsProvisioningConfiguration");

            configurationSet.ComputerName = computerName;
            configurationSet.AdminUserName = adminUserName;
            configurationSet.AdminPassword = adminPassword;
            configurationSet.ResetPasswordOnFirstLogon |= resetPasswordOnFirstLogon;
            configurationSet.EnableAutomaticUpdates |= enableAutomaticUpdates;

            // TimeZone = "",
            // DomainJoin = new DomainJoinSettings { },
            // StoredCertificateSettings = new List<StoredCertificateSettings> { new StoredCertificateSettings { StoreName = "My", Thumbprint = "..." }},
            // WindowsRemoteManagement = new WindowsRemoteManagementSettings { Listeners = new List<WindowsRemoteManagementListener> { new WindowsRemoteManagementListener { ListenerType = VirtualMachineWindowsRemoteManagementListenerType.Https, CertificateThumbprint = "... "}}},
            // CustomData = System.Convert.ToBase64String(), // Optional in WindowsProvisioningConfiguration. Specifies a base-64 encoded string of custom data. The base-64 encoded string is decoded to a binary array that is saved as a file on the Virtual Machine. The maximum length of the binary array is 65535 bytes. The file is saved to %SYSTEMDRIVE%\AzureData\CustomData.bin. If the file exists, it is overwritten. The security on directory is set to System:Full Control and Administrators:Full Control.

            return role;
        }

        public static Role AddLinuxProvisioningConfiguration(this Role role,
            string hostName, string userName, string password)
        {
            var configurationSet = role.GetConfigurationSet("LinuxProvisioningConfiguration");

            configurationSet.HostName = hostName;
            configurationSet.UserName = userName;
            configurationSet.UserPassword = password;

            return role;
        }

        public static Role AddInputEndpoint(this Role role, InputEndpoint inputEndpoint)
        {
            var configurationSet = role.GetConfigurationSet("NetworkConfiguration");

            if (configurationSet.InputEndpoints == null) { configurationSet.InputEndpoints = new List<InputEndpoint>(); }

            var existingInputEndpoint = configurationSet.InputEndpoints.FirstOrDefault(_ => _.Name == inputEndpoint.Name);
            if (existingInputEndpoint != null) { configurationSet.InputEndpoints.Remove(existingInputEndpoint); }

            configurationSet.InputEndpoints.Add(inputEndpoint);

            return role;
        }

        public static Role AddPublicIPs(this Role role, ConfigurationSet.PublicIP publicIP)
        {
            var configurationSet = role.GetConfigurationSet("NetworkConfiguration");

            if (configurationSet.PublicIPs == null) { configurationSet.PublicIPs = new List<ConfigurationSet.PublicIP>(); }

            if (!configurationSet.PublicIPs.Contains(publicIP)) { configurationSet.PublicIPs.Add(publicIP); }

            return role;
        }

        public static Role AddExtension(this Role role, ResourceExtensionReference reference) 
        {
            if (role.ResourceExtensionReferences == null) { role.ResourceExtensionReferences = new List<ResourceExtensionReference>(); }

            var existingExtension = role.ResourceExtensionReferences.FirstOrDefault(_ => _.ReferenceName == reference.ReferenceName);
            if (existingExtension != null) { role.ResourceExtensionReferences.Remove(existingExtension); }

            role.ResourceExtensionReferences.Add(reference);

            return role;
        }

        public static Role AddBGInfoExtension(this Role role)
        {
            return role.AddExtension(new ResourceExtensionReference
            {
                State = "Enable",
                Name = "BGInfo",
                ReferenceName = "BGInfo",
                Publisher = "Microsoft.Compute",
                Version = "1.*"
            });
        }

        internal class PrivateConfigParameter
        {
            public string storageAccountName { get; set; }
            public string storageAccountKey { get; set; }
        }

        internal class PublicConfigParameter
        {
            public string[] fileUris { get; set; }
            public string commandToExecute { get; set; }
        }

        public static Role AddCustomScriptExtension(this Role role, 
            CloudStorageAccount storageAccount, string containerName, string filename, string arguments)
        {
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlockBlob script = blobClient.GetContainerReference(containerName).GetBlockBlobReference(filename);

            var publicCfg = new PublicConfigParameter
            { 
                fileUris = new[] { string.Format("{0}{1}", 
                    script.Uri.ToString(), 
                    script.GetSharedAccessSignature(
                        new SharedAccessBlobPolicy
                        {
                            Permissions = SharedAccessBlobPermissions.Read,
                            SharedAccessExpiryTime = DateTime.UtcNow.Add(TimeSpan.FromMinutes(50))
                        })) },
                commandToExecute = string.Format(
                    "powershell -ExecutionPolicy Unrestricted -file {0} {1}", 
                    script.Name, arguments)
                             
            };

            var privateCfg = new PrivateConfigParameter
            {
                storageAccountName = storageAccount.Credentials.AccountName,
                storageAccountKey = storageAccount.Credentials.ExportBase64EncodedKey()
            };

            return role.AddExtension(new ResourceExtensionReference
            {
                Publisher = "Microsoft.Compute",
                ReferenceName = "CustomScriptExtension",
                Name = "CustomScriptExtension",
                Version = "1.*",
                State = "Enable",
                ResourceExtensionParameterValues = new List<ResourceExtensionParameterValue> 
                {
                    new ResourceExtensionParameterValue { Key = "CustomScriptExtensionPublicConfigParameter", Value = publicCfg.ToBase64EncodedJson(), Type = "Public", },
                    new ResourceExtensionParameterValue { Key = "CustomScriptExtensionPrivateConfigParameter", Value = privateCfg.ToBase64EncodedJson(), Type = "Private" }
                }
            });
        }

        public static string ToBase64EncodedJson(this object value)
        {
            var json = JsonConvert.SerializeObject(value);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }

        public static async Task<CloudStorageAccount> ToCloudStorageAccountAsync(this StorageManagementClient managementClient, StorageAccount storageAccountFromManagementAPI)
        {
            var keys = await managementClient.StorageAccounts.GetKeysAsync(storageAccountFromManagementAPI.Name);

            return new CloudStorageAccount(new StorageCredentials(storageAccountFromManagementAPI.Name, keys.PrimaryKey), useHttps: true);
        }

        public static X509Certificate2 FindX509CertificateByThumbprint(this string managementCertThumbprint)
        {
            return FindX509Certificate(managementCertThumbprint,
                X509FindType.FindByThumbprint,
                StoreName.My,
                StoreLocation.LocalMachine);
        }

        public static X509Certificate2 FindX509Certificate(this string findValue, X509FindType findType, StoreName storeName, StoreLocation storeLocation)
        {
            X509Store store = null;

            try
            {
                store = new X509Store(
                    storeName: storeName,
                    storeLocation: storeLocation);
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                X509Certificate2Collection certs = store.Certificates.Find(
                    findType: findType,
                    findValue: findValue,
                    validOnly: false);
                if (certs.Count == 0)
                {
                    return null;
                }

                return certs[0];
            }
            finally
            {
                if (store != null) store.Close();
            }
        }
    }
}