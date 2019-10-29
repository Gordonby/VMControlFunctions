using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;

namespace VMControlFunctionsv2
{
    public static class StartVM
    {
        [FunctionName("StartVM")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {

            try
            {


                log.LogInformation("Getting request body params");
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);
                string subscriptionId = data?.subscriptionId;
                string resourceGroupName = data?.resourceGroupName;
                bool useVmStartAwait = data?.wait == null ? false ? data?.wait;
                string vmName = data?.vmName;

                if (subscriptionId == null || resourceGroupName == null || vmName == null)
                {
                    return new BadRequestObjectResult("Please pass all 3 required parameters in the request body");
                }

                log.LogInformation("Setting authentication to use MSI");
                AzureCredentialsFactory f = new AzureCredentialsFactory();
                var msi = new MSILoginInformation(MSIResourceType.AppService);

                var msiCred = f.FromMSI(msi, AzureEnvironment.AzureGlobalCloud);

                var azureAuth = Azure.Configure()
                                 .WithLogLevel(HttpLoggingDelegatingHandler.Level.BodyAndHeaders)
                                 .Authenticate(msiCred);

                log.LogInformation("Authenticating with Azure using MSI");
                var azure = azureAuth.WithSubscription(subscriptionId);

                log.LogInformation("Acquiring VM from Azure");
                var vm = azure.VirtualMachines.GetByResourceGroup(resourceGroupName, vmName);

                log.LogInformation("Checking VM Id");
                log.LogInformation(vm.Id.ToString());

                log.LogInformation("Checking VM Powerstate");
                log.LogInformation("VM Powerstate : " + vm.PowerState.ToString());

                bool vmStarting = false;
                if (vm.PowerState.ToString() == "PowerState/running")
                {
                    log.LogInformation("VM is already running");
                }
                else
                {
                    if (useVmStartAwait)
                    {
                        log.LogInformation("Async Starting vm " + vmName);
                        await vm.StartAsync();
                    }
                    else
                    {
                        log.LogInformation("Sync Starting vm " + vmName);
                        vm.Start();
                    }

                    vmStarting = true;
                }

                return vmStarting == false
                ? (ActionResult)new OkObjectResult("VM was already started")
                : (ActionResult)new OkObjectResult("VM started");

            }
            catch (System.Exception ex)
            {
                log.LogError(ex.Message);
                throw;
            }
        }
    }
}
