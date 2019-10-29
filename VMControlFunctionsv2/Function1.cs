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
    public static class Function1
    {
        [FunctionName("StartVM")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            //log.LogInformation("C# HTTP trigger function processed a request.");

            //string name = req.Query["name"];

            //string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            //dynamic data = JsonConvert.DeserializeObject(requestBody);
            //name = name ?? data?.name;

            //return name != null
            //    ? (ActionResult)new OkObjectResult($"Hello, {name}")
            //    : new BadRequestObjectResult("Please pass a name on the query string or in the request body");

            try
            {


                log.LogInformation("Getting request body params");
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);
                string subscriptionId = data?.subscriptionId;
                string resourceGroupName = data?.resourceGroupName;
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
                    log.LogInformation("Starting vm " + vmName);
                    await vm.StartAsync();
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
