using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SharpCompress.Common;
using SharpCompress.Readers;
using Microsoft.Extensions.Configuration;
using Azure.Storage.Blobs;
using Microsoft.Azure.Services.AppAuthentication;
using Azure.Core;
using Azure.Identity;
using System.Text;
using SharpCompress.Archives.Rar;
using System.Text.RegularExpressions;

namespace BlobCompressionTools
{
    public static class Unrar
    {
        [FunctionName("Unrar")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            log.LogInformation("UnRar function has been called by http trigger");

            #region Initialization
            //configurations
            var config = new ConfigurationBuilder()
                    .SetBasePath(context.FunctionAppDirectory)
                    .AddJsonFile("settings.json",optional:true, reloadOnChange:true)
                    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build();


            string storageAccountConnectionString = string.Empty;
            string storageAccountName = string.Empty;
            string storageAccessToken = string.Empty;
            BlobClient clientSrc = null;
            BlobClient clientDest = null;
            #endregion


            #region Input and validation
            //read the request body into string
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            log.LogTrace($"Request body: {requestBody}");
            //deserialize body into Models.BlobInfo object
            var reqBlobInfo = JsonConvert.DeserializeObject<Models.BlobInfo>(requestBody);


            //storage account name must be provided
            storageAccountName = config["StorageAccountName"];

            //container source & destination can be sent in the body or obtained from configurations
            if (String.IsNullOrEmpty(reqBlobInfo.containerSource))
            {
                //get the value from configurations
                reqBlobInfo.containerSource = config["ContainerNameSource"];
            }
            if (String.IsNullOrEmpty(reqBlobInfo.containerTarget))
            {
                //get the value from configurations
                reqBlobInfo.containerTarget = config["ContainerNameTarget"];
            }
            if(reqBlobInfo.useManagedIdentity==false) //has to be provided if not using managed identity
            {
                //get the value from configurations
                storageAccountConnectionString = config["StorageAccountConnectionString"];
            }

            if(String.IsNullOrEmpty(reqBlobInfo.fileName) | String.IsNullOrEmpty(reqBlobInfo.containerSource) | String.IsNullOrEmpty(reqBlobInfo.containerTarget) | (String.IsNullOrEmpty(storageAccountConnectionString) & reqBlobInfo.useManagedIdentity==false))
            {
                //return error 
                log.LogError("Missing fileName or the source or destination container or storage Account name");
                return new BadRequestObjectResult("MISSING_INPUT");
            }

            #endregion


            //prepare local disk workspace
            var localFolder = Path.Combine(Path.GetTempPath().Replace("C:", "D:"), "Compression",context.InvocationId.ToString());
            DirectoryInfo dir= Directory.CreateDirectory(localFolder);
            


            //Get the blob
            if(reqBlobInfo.useManagedIdentity)
            {

                string filePath = $"https://{storageAccountName}.blob.core.windows.net/{reqBlobInfo.containerSource}/{reqBlobInfo.fileName}";
                clientSrc = new BlobClient(new Uri(filePath), new DefaultAzureCredential());

            }
            else
            {
                //using keys
                clientSrc = new BlobClient(storageAccountConnectionString, reqBlobInfo.containerSource, reqBlobInfo.fileName);
            }
           
            
            
            if (await clientSrc.ExistsAsync())
            {
                string localFilePath = $@"{localFolder}{clientSrc.Name}";
                var response = await clientSrc.DownloadToAsync(localFilePath);
                log.LogTrace($"Downloaded blob with status code {response.Status.ToString()}");

                //start unrar
                var rarReaderOptions = new ReaderOptions()
                {
                    ArchiveEncoding = new ArchiveEncoding(Encoding.UTF8, Encoding.UTF8),
                    LookForHeader = true
                };

                using var reader = RarArchive.Open(localFilePath, rarReaderOptions);
                //loop through the files in the package and decompress then upload them
                foreach (RarArchiveEntry entry in reader.Entries)
                {
                    string validName = entry.Key;
                    //Replace all NO digits, letters, or "-" by a "-" Azure storage is specific on valid characters
                    validName = Regex.Replace(validName, @"[^a-zA-Z0-9\-\.\\]", "-").ToLower();
                    if (!entry.IsDirectory)
                    {
                        if (reqBlobInfo.useManagedIdentity)
                        {

                            string filePath = $"https://{storageAccountName}.blob.core.windows.net/{reqBlobInfo.containerTarget}/{validName}";
                            clientDest = new BlobClient(new Uri(filePath), new DefaultAzureCredential());

                        }
                        else
                        {
                            //using keys
                            clientDest = new BlobClient(storageAccountConnectionString, reqBlobInfo.containerTarget, validName);
                        }


                        using (var fileStream = entry.OpenEntryStream())
                        {
                            await clientDest.UploadAsync(fileStream, true);
                        }
                    }
                }
                //empty local directory
                dir.Delete(true);

            }
            else
            {
                log.LogError("Blob does not exist");
                return new BadRequestObjectResult("BLOB_NOT_EXIST");
            }

            return new OkResult();
        }


       
    }
}
