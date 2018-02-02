﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using TollBooth.Models;

namespace TollBooth
{
    internal class FileMethods
    {
        private readonly CloudBlobClient _blobClient;
        private readonly string _containerName = ConfigurationManager.AppSettings["exportCsvContainerName"];
        private readonly string _blobStorageConnection = ConfigurationManager.AppSettings["blobStorageConnection"];
        private readonly TraceWriter _log;

        public FileMethods(TraceWriter log)
        {
            _log = log;
            // Retrieve storage account information from connection string.
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(_blobStorageConnection);

            // Create a blob client for interacting with the blob service.
            _blobClient = storageAccount.CreateCloudBlobClient();
        }

        public async Task<bool> GenerateAndSaveCsv(IEnumerable<LicensePlateDataDocument> licensePlates)
        {
            var successful = false;

            _log.Info("Generating CSV file");
            string blobName = $"{DateTime.UtcNow.ToString("s")}.csv";

            using (var stream = new MemoryStream())
            {
                using (var textWriter = new StreamWriter(stream))
                using (var csv = new CsvWriter(textWriter))
                {
                    csv.Configuration.Delimiter = ",";
                    csv.WriteRecords(licensePlates.Select(ToLicensePlateData));
                    await textWriter.FlushAsync();

                    _log.Info($"Beginning file upload: {blobName}");
                    try
                    {
                        var container = _blobClient.GetContainerReference(_containerName);

                        // Retrieve reference to a blob.
                        var blob = container.GetBlockBlobReference(blobName);
                        await container.CreateIfNotExistsAsync();

                        // Upload blob.
                        stream.Position = 0;
                        //  7: Asyncronously upload the blob from the memory stream.
                        // COMPLETE: await blob...;
                        await blob.UploadFromStreamAsync(stream);
                        successful = true;
                    }
                    catch (Exception e)
                    {
                        _log.Error($"Could not upload CSV file: {e.Message}", e);
                        successful = false;
                    }
                }
            }

            return successful;
        }

        /// <summary>
        /// Used for mapping from a LicensePlateDataDocument object to a LicensePlateData object.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        private static LicensePlateData ToLicensePlateData(LicensePlateDataDocument source)
        {
            return new LicensePlateData
            {
                FileName = source.fileName,
                LicensePlateText = source.licensePlateText,
                TimeStamp = source.Timestamp
            };
        }
    }
}
