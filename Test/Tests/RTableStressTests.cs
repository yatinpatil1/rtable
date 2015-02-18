﻿//////////////////////////////////////////////////////////////////////////////
//
// Copyright (c) Microsoft Corporation. All rights reserved.
//
//////////////////////////////////////////////////////////////////////////////

namespace Microsoft.Azure.Toolkit.Replication.Test
{
    using Microsoft.Azure.Toolkit.Replication;
    using Microsoft.WindowsAzure.Storage.Table;
    using Microsoft.WindowsAzure.Storage.Table.Queryable;
    using NUnit.Framework;
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Collections.Generic;

    [TestFixture]
    public class RTableStressTests : RTableWrapperTestBase
    {
        //Run this test against two replicas
        private const int HeadReplicaAccountIndex = 0;
        private const int TailReplicaAccountIndex = 1;

        //Number of entities in the table
        private const int NumberofEntities = 10;

        //Number of operations run
        private const int NumberOfOperations = 10;

        private const string JobType = "jobType-RandomTableOperationTest";
        private const string JobId = "jobId-RandomTableOperationTest";
        private const string OriginalMessage = "message";

        [TestFixtureSetUp]
        public void TestFixtureSetup()
        {
            this.LoadTestConfiguration();

            bool reUploadRTableConfigBlob = true;
            string tableName = this.GenerateRandomTableName();
            bool useHttps = true;
            long viewId = 1;
            string viewIdString = viewId.ToString();
            var actualStorageAccountsToBeUsed = new List<int>() { HeadReplicaAccountIndex, TailReplicaAccountIndex };
            bool convertXStoreTableMode = false;

            Console.WriteLine("Setting up RTable that has a Head Replica and a Tail Replica...");
            this.SetupRTableEnv(
                reUploadRTableConfigBlob,
                tableName,
                useHttps,
                viewIdString,
                actualStorageAccountsToBeUsed,
                convertXStoreTableMode);
            Assert.AreEqual(2, this.actualStorageAccountsUsed.Count, "Two storage accounts should be used at this point.");

            //Fill up the table with the specified number of entities
            Console.WriteLine("Inserting entities to the RTable...");
            this.numberOfPartitions = 1;
            this.numberOfRowsPerPartition = NumberofEntities;
            this.PerformInsertOperationAndValidate(JobType, JobId, OriginalMessage);
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            base.DeleteAllRtableResources();
        }

        [Test(Description = "Randomly performs operations on multiple replicas and validates the tables.")]
        public void RandomTableOperationTest()
        {   
            const int partitionIndex = 0;
            // Insert some entries. 

            //Key = RowNumber, Value = RowKey
            var currentEntities = new Dictionary<int, String>();
            for (int i = 0; i < this.numberOfRowsPerPartition; i++)
            {
                currentEntities.Add(i, GenerateJobId(JobId, partitionIndex, i));
            }

            //Key = RowNumber, Value = RowKey
            var deletedEntities = new Dictionary<int, String>();

            var random = new Random();
            for (int i = 0; i < NumberOfOperations; i++)
            {
                int operation = random.Next(0, Enum.GetNames(typeof(TableOperationType)).Count());
                int randomRow = random.Next(0, NumberofEntities);

                if ((TableOperationType) operation == TableOperationType.Insert)
                {
                    if (deletedEntities.Count == 0)
                    {
                        //Ignore insertion if there is nothing to be inserted
                        continue;
                    }
                    randomRow = deletedEntities.First().Key;
                    deletedEntities.Remove(randomRow);
                }

                if (deletedEntities.ContainsKey(randomRow))
                {
                    continue;
                }

                Console.WriteLine("Operation# {0}, {1} on row {2}", i, (TableOperationType)operation, randomRow);
                PerformIndividualOperationAndValidate((TableOperationType)operation, JobType, JobId, partitionIndex, randomRow, OriginalMessage);

                if ((TableOperationType)operation == TableOperationType.Delete)
                {
                    deletedEntities.Add(randomRow, GenerateJobId(JobId, partitionIndex, randomRow));
                }
            }

            string rowKey;
            string partitionKey;
            SampleRTableEntity.GenerateKeys(this.GenerateJobType(JobType, partitionIndex), "don't care", out partitionKey, out rowKey);

            //Validations
            Console.WriteLine("Performing replica validations");
            PerformInvariantChecks(partitionKey, HeadReplicaAccountIndex, TailReplicaAccountIndex);
            Console.WriteLine("DONE. Test passed.");
        }
    }
}
