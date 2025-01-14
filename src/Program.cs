﻿
using Azure;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AzureSearch.SDKHowTo
{
    public sealed class Program
    {
        // This sample shows how to create a search index, and how to index and merge data
        // from two different data sources.

        static IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
        static IConfigurationRoot configuration = builder.Build();

        static async Task Main(string[] args)
        {
            string searchServiceUri = configuration["SearchServiceUri"];
            string adminApiKey = configuration["SearchServiceAdminApiKey"];

            SearchIndexClient indexClient = new SearchIndexClient(new Uri(searchServiceUri), new AzureKeyCredential(adminApiKey));
            SearchIndexerClient indexerClient = new SearchIndexerClient(new Uri(searchServiceUri), new AzureKeyCredential(adminApiKey));

            string indexName = "globalindex-sample";

            // Next, create the search index
            Console.WriteLine("Deleting index...\n");
            await DeleteIndexIfExistsAsync(indexName, indexClient);

            Console.WriteLine("Creating index...\n");
            await CreateIndexAsync(indexName, indexClient);

            //Set up a SQLDb data source and indexer, and run the indexer to import data
           Console.WriteLine("Indexing SQL data...\n");
           await CreateAndRunSqlDbIndexerAsync(indexName, indexerClient);

            // Set up a CosmosDB data source and indexer, and run the indexer to import hotel data
            Console.WriteLine("Indexing Cosmos DB hotel data...\n");
            await CreateAndRunCosmosDbIndexerAsync(indexName, indexerClient);


            // Set up a Blob Storage data source and indexer, and run the indexer to merge hotel room data
          //  Console.WriteLine("Indexing blob storage data...\n");
          //  await CreateAndRunBlobIndexerAsync(indexName, indexerClient);

            Console.WriteLine("Complete.  Press any key to end application...\n");
            Console.ReadKey();
        }

        private static async Task DeleteIndexIfExistsAsync(string indexName, SearchIndexClient indexClient)
        {
            try
            {
                await indexClient.GetIndexAsync(indexName);
                await indexClient.DeleteIndexAsync(indexName);
            }
            catch (RequestFailedException ex) when (ex.Status == 404) 
            {
                //if the specified index not exist, 404 will be thrown.
            }
        }

        private static async Task CreateIndexAsync(string indexName, SearchIndexClient indexClient)
        {
            // Create a new search index structure that matches the properties of the Hotel class.
            // The Address and Room classes are referenced from the Hotel class. The FieldBuilder
            // will enumerate these to create a complex data structure for the index.
            FieldBuilder bulder = new FieldBuilder();
            var definition = new SearchIndex(indexName, bulder.Build(typeof(Entity)));

            await indexClient.CreateIndexAsync(definition);
        }

        private static async Task CreateAndRunSqlDbIndexerAsync(string indexName, SearchIndexerClient indexerClient)
        {
            // Append the database name to the connection string
            string sqlConnectString =
                configuration["SqlDBConnectionString"]
                + ";Database="
                + configuration["SqlDBDatabaseName"];

            SearchIndexerDataSourceConnection sqlDbDataSource = new SearchIndexerDataSourceConnection(
                name: configuration["SqlDBDatabaseName"],
                type: SearchIndexerDataSourceType.AzureSql,
                connectionString: sqlConnectString,
                container: new SearchIndexerDataContainer("entity_details_poc_1"));

            // The Cosmos DB data source does not need to be deleted if it already exists,
            // but the connection string might need to be updated if it has changed.            
            await indexerClient.CreateOrUpdateDataSourceConnectionAsync(sqlDbDataSource);

            Console.WriteLine("Creating SQL DB indexer...\n");

            // Add a field mapping to match the globalid field in the documents to 
            // the DocId key field in the index

            SearchIndexer sqlDbIndexer = new SearchIndexer(
                name: "sql-indexer",
                dataSourceName: sqlDbDataSource.Name,
                targetIndexName: indexName)
            {
                Schedule = new IndexingSchedule(TimeSpan.FromDays(1))
            };


            // Indexers keep metadata about how much they have already indexed.
            // If we already ran this sample, the indexer will remember that it already
            // indexed the sample data and not run again.
            // To avoid this, reset the indexer if it exists.   
            try
            {
                await indexerClient.GetIndexerAsync(sqlDbIndexer.Name);
                //Rest the indexer if it exsits.
                await indexerClient.ResetIndexerAsync(sqlDbIndexer.Name);
            }
            catch (RequestFailedException ex) when (ex.Status == 404) 
            {
                //if the specified indexer not exist, 404 will be thrown.
            }

            await indexerClient.CreateOrUpdateIndexerAsync(sqlDbIndexer);

            Console.WriteLine("Running SQL DB indexer...\n");

            try
            {
                await indexerClient.RunIndexerAsync(sqlDbIndexer.Name);
            }
            catch (RequestFailedException ex) when (ex.Status == 429)
            {
                Console.WriteLine("Failed to run indexer: {0}", ex.Message);
            }
        }

        private static async Task CreateAndRunCosmosDbIndexerAsync(string indexName, SearchIndexerClient indexerClient)
        {
            // Append the database name to the connection string
            string cosmosConnectString =
                configuration["CosmosDBConnectionString"]
                + ";Database="
                + configuration["CosmosDBDatabaseName"];

            SearchIndexerDataSourceConnection cosmosDbDataSource = new SearchIndexerDataSourceConnection(
                name: configuration["CosmosDBDatabaseName"],
                type: SearchIndexerDataSourceType.CosmosDb,
                connectionString: cosmosConnectString,
                container: new SearchIndexerDataContainer("casecollection"));

            // The Cosmos DB data source does not need to be deleted if it already exists,
            // but the connection string might need to be updated if it has changed.            
            await indexerClient.CreateOrUpdateDataSourceConnectionAsync(cosmosDbDataSource);

            Console.WriteLine("Creating Cosmos DB indexer...\n");


                List<FieldMapping> map = new List<FieldMapping> {

                new FieldMapping("entity_key")
                {
                    TargetFieldName =  "uniqueid"
                }
            };

            SearchIndexer cosmosDbIndexer = new SearchIndexer(
                name: "cosmos-indexer",
                dataSourceName: cosmosDbDataSource.Name,
                targetIndexName: indexName)
            {
                Schedule = new IndexingSchedule(TimeSpan.FromDays(1))
            };

            // Setup the mappings in the Cosmos DB Indexer
           
            foreach (FieldMapping fieldMapping in map)
            {
                cosmosDbIndexer.FieldMappings.Add(fieldMapping);
            }

            // Indexers keep metadata about how much they have already indexed.
            // If we already ran this sample, the indexer will remember that it already
            // indexed the sample data and not run again.
            // To avoid this, reset the indexer if it exists.   
            try
            {
                await indexerClient.GetIndexerAsync(cosmosDbIndexer.Name);
                //Rest the indexer if it exsits.
                await indexerClient.ResetIndexerAsync(cosmosDbIndexer.Name);
            }
            catch (RequestFailedException ex) 
            
            when (ex.Status == 404) 
            {
                //if the specified indexer not exist, 404 will be thrown.
            }

            await indexerClient.CreateOrUpdateIndexerAsync(cosmosDbIndexer);

            Console.WriteLine("Running Cosmos DB indexer...\n");

            try
            {
                await indexerClient.RunIndexerAsync(cosmosDbIndexer.Name);
            }
            catch (RequestFailedException ex) when (ex.Status == 429)
            {
                Console.WriteLine("Failed to run indexer: {0}", ex.Message);
            }
        }
      

       /* private static async Task CreateAndRunBlobIndexerAsync(string indexName, SearchIndexerClient indexerClient)
        {
            SearchIndexerDataSourceConnection blobDataSource = new SearchIndexerDataSourceConnection(
                name: configuration["BlobStorageAccountName"],
                type: SearchIndexerDataSourceType.AzureBlob,
                connectionString: configuration["BlobStorageConnectionString"],
                container: new SearchIndexerDataContainer("hotel-rooms"));

            // The blob data source does not need to be deleted if it already exists,
            // but the connection string might need to be updated if it has changed.
            await indexerClient.CreateOrUpdateDataSourceConnectionAsync(blobDataSource);

            Console.WriteLine("Creating Blob Storage indexer...\n");

            // Add a field mapping to match the Id field in the documents to 
            // the HotelId field in the index, and globalid to the 
            // DocID key field in the index.
            List<FieldMapping> map = new List<FieldMapping> {
                new FieldMapping("Id")
                {
                    TargetFieldName =  "HotelId"
                },
                new FieldMapping("globalid")
                {
                    TargetFieldName =  "DocId"
                }
            };

            IndexingParameters parameters = new IndexingParameters();
            parameters.Configuration.Add("parsingMode", "json");

            SearchIndexer blobIndexer = new SearchIndexer(
                name: "hotel-rooms-blob-indexer",
                dataSourceName: blobDataSource.Name,
                targetIndexName: indexName)
            {
                Parameters = parameters,
                Schedule = new IndexingSchedule(TimeSpan.FromDays(1))
            };

            // Setup the mappings in the Blob DB Indexer
            foreach (FieldMapping fieldMapping in map)
            {
                blobIndexer.FieldMappings.Add(fieldMapping);
            }


            // Reset the indexer if it already exists
            try
            {
                await indexerClient.GetIndexerAsync(blobIndexer.Name);
                //Rest the indexer if it exsits.
                await indexerClient.ResetIndexerAsync(blobIndexer.Name);
            }
            catch (RequestFailedException ex) when (ex.Status == 404) { }

            await indexerClient.CreateOrUpdateIndexerAsync(blobIndexer);

            Console.WriteLine("Running Blob Storage indexer...\n");

            try
            {
                await indexerClient.RunIndexerAsync(blobIndexer.Name);
            }
            catch (RequestFailedException ex) when (ex.Status == 429)
            {
                Console.WriteLine("Failed to run indexer: {0}", ex.Message);
            }
        }*/
    }
}
