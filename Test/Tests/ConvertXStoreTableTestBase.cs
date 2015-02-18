﻿//////////////////////////////////////////////////////////////////////////////
//
// Copyright (c) Microsoft Corporation. All rights reserved.
//
//////////////////////////////////////////////////////////////////////////////



namespace Microsoft.Azure.Toolkit.Replication.Test
{
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;
    using NUnit.Framework;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;

    /// <summary>
    /// Base class for ConvertXStorTable tests.
    /// Provider help function to set up an XStore Table with entities so that
    /// the test cases can perform operation on those entities and convert them to RTable entities.
    /// </summary>
    public class ConvertXStoreTableTestBase : RTableWrapperTestBase
    {
        /// <summary>
        /// Actual name of the XStore Table that will be created and then converted (to RTable)
        /// </summary>
        protected string xstoreTableName;

        private const string XStoreTablePrefix = "XStoreTable";
        
        private string xstoreConnectionString;

        private CloudTableClient xstoreCloudTableClient;

        private CloudTable xstoreCloudTable;

        #region Helper functions
        protected void SetupXStoreTableEnv()
        {
            this.LoadTestConfiguration();
            this.xstoreTableName = this.GenerateRandomTableName(XStoreTablePrefix);

            int numberOfStorageAccounts = this.rtableTestConfiguration.StorageInformation.AccountNames.Count();

            // Hard coded that account #0 will be used in this set of tests.
            this.actualStorageAccountsUsed = new List<int> { 0 };

            int xstoreAccountIndex = this.actualStorageAccountsUsed[this.actualStorageAccountsUsed.Count - 1];

            this.xstoreConnectionString = this.GetConnectionString(
                                   this.rtableTestConfiguration.StorageInformation.AccountNames[xstoreAccountIndex],
                                   this.rtableTestConfiguration.StorageInformation.AccountKeys[xstoreAccountIndex],
                                   this.rtableTestConfiguration.StorageInformation.DomainName);

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(this.xstoreConnectionString);
            this.xstoreCloudTableClient = storageAccount.CreateCloudTableClient();
            this.xstoreCloudTable = this.xstoreCloudTableClient.GetTableReference(this.xstoreTableName);
            this.xstoreCloudTable.CreateIfNotExists();
        }

        /// <summary>
        /// Insert some entities into the XStore Table.
        /// There will be "numberOfPartitions" partitions
        /// and "numberOfRowsPerPartition" rows per partition.
        /// Assumption is that those entities do NOT exist currently.
        /// </summary>
        /// <param name="jobType"></param>
        /// <param name="jobId"></param>
        /// <param name="entityMessage"></param>
        protected void InsertXStoreEntities(
            string jobType,
            string jobId,
            string entityMessage)
        {
            for (int i = 0; i < this.numberOfPartitions; i++)
            {
                for (int j = 0; j < this.numberOfRowsPerPartition; j++)
                {
                    SampleXStoreEntity sampleXStoreEntity = new SampleXStoreEntity(
                        this.GenerateJobType(jobType, i),
                        this.GenerateJobId(jobId, i, j),
                        this.GenerateMessage(entityMessage, i, j));

                    //
                    // Insert
                    //
                    TableOperation insertOperation = TableOperation.Insert(sampleXStoreEntity);
                    TableResult insertResult = this.xstoreCloudTable.Execute(insertOperation);

                    Assert.IsNotNull(insertResult, "insertResult = null");
                    Assert.AreEqual((int)HttpStatusCode.NoContent, insertResult.HttpStatusCode, "entry #{0} row {1}: insertResult.HttpStatusCode mismatch", i, j);
                    Assert.IsFalse(string.IsNullOrEmpty(insertResult.Etag), "partition #{0} row {1}: insertResult.ETag = null or empty", i, j);

                    ITableEntity row = (ITableEntity)insertResult.Result;
                    //
                    // Retrieve
                    //
                    TableOperation retrieveOperation = TableOperation.Retrieve<SampleXStoreEntity>(row.PartitionKey, row.RowKey);
                    TableResult retrieveResult = this.xstoreCloudTable.Execute(retrieveOperation);

                    Assert.IsNotNull(retrieveResult, "retrieveResult = null");
                    Assert.AreEqual((int)HttpStatusCode.OK, retrieveResult.HttpStatusCode, "partition #{0} row {1}: retrieveResult.HttpStatusCode mismatch", i, j);
                    SampleXStoreEntity retrievedEntity = (SampleXStoreEntity)retrieveResult.Result;
                    Assert.IsTrue(sampleXStoreEntity.Equals(retrievedEntity), "entry #{0} row {1}: sampleXStoreEntity != retrievedEntity");
                }
            }
        }

        /// <summary>
        /// Perform XStore Replace operations on the specified entities.
        /// </summary>
        /// <param name="jobType"></param>
        /// <param name="jobId"></param>
        /// <param name="entityMessage"></param>
        protected void ReplaceXStoreEntities(
            string jobType,
            string jobId,
            string entityMessage)
        {
            for (int i = 0; i < this.numberOfPartitions; i++)
            {
                for (int j = 0; j < this.numberOfRowsPerPartition; j++)
                {
                    //
                    // Retrieve
                    //
                    string partitionKey;
                    string rowKey;
                    SampleXStoreEntity.GenerateKeys(
                        this.GenerateJobType(jobType, i),
                        this.GenerateJobId(jobId, i, j),
                        out partitionKey,
                        out rowKey);
                    TableOperation retrieveOperation = TableOperation.Retrieve<SampleXStoreEntity>(partitionKey, rowKey);
                    TableResult retrieveResult = this.xstoreCloudTable.Execute(retrieveOperation);

                    Assert.IsNotNull(retrieveResult, "retrieveResult = null");
                    SampleXStoreEntity retrievedEntity = (SampleXStoreEntity)retrieveResult.Result;
                    retrievedEntity.Message = this.GenerateMessage(entityMessage, i, j);
                    //
                    // Replace
                    //
                    TableOperation replaceOperation = TableOperation.Replace(retrievedEntity);
                    TableResult replaceResult = this.xstoreCloudTable.Execute(replaceOperation);
                    Assert.IsNotNull(replaceResult, "replaceResult = null");
                    Assert.AreEqual((int)HttpStatusCode.NoContent, replaceResult.HttpStatusCode, "entry #{0} row {1}: insertResult.HttpStatusCode mismatch", i, j);

                    //
                    // Retrieve again
                    //
                    retrieveOperation = TableOperation.Retrieve<SampleXStoreEntity>(partitionKey, rowKey);
                    retrieveResult = this.xstoreCloudTable.Execute(retrieveOperation);
                    Assert.IsNotNull(retrieveResult, "After Replace(): retrieveResult = null");
                    SampleXStoreEntity replacedEntity = (SampleXStoreEntity)retrieveResult.Result;

                    Assert.AreEqual(
                        this.GenerateJobType(jobType, i),
                        replacedEntity.JobType,
                        "After Replace(): JobType mismatch");
                    Assert.AreEqual(
                        this.GenerateJobId(jobId, i, j),
                        replacedEntity.JobId,
                        "After Replace(): JobId mismatch");
                    Assert.AreEqual(
                        this.GenerateMessage(entityMessage, i, j),
                        replacedEntity.Message,
                        "After Replace(): Message mismatch");
                }
            }
        }

        #endregion Helper functions
    }
}
