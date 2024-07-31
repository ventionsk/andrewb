﻿namespace Wexflow.Core.Db.MariaDB
{
    public class StatusCount : Core.Db.StatusCount
    {
        public const string COLUMN_NAME_ID = "ID";
        public const string COLUMN_NAME_PENDING_COUNT = "PENDING_COUNT";
        public const string COLUMN_NAME_RUNNING_COUNT = "RUNNING_COUNT";
        public const string COLUMN_NAME_DONE_COUNT = "DONE_COUNT";
        public const string COLUMN_NAME_FAILED_COUNT = "FAILED_COUNT";
        public const string COLUMN_NAME_WARNING_COUNT = "WARNING_COUNT";
        public const string COLUMN_NAME_DISABLED_COUNT = "DISABLED_COUNT";
        public const string COLUMN_NAME_STOPPED_COUNT = "STOPPED_COUNT";
        public const string COLUMN_NAME_REJECTED_COUNT = "DISAPPROVED_COUNT";

        public const string TABLE_STRUCT = "(" + COLUMN_NAME_ID + " INT NOT NULL AUTO_INCREMENT, " + COLUMN_NAME_PENDING_COUNT + " INT, " + COLUMN_NAME_RUNNING_COUNT + " INT, " + COLUMN_NAME_DONE_COUNT + " INT, " + COLUMN_NAME_FAILED_COUNT + " INT, " + COLUMN_NAME_WARNING_COUNT + " INT, " + COLUMN_NAME_DISABLED_COUNT + " INT, " + COLUMN_NAME_STOPPED_COUNT + " INT, " + COLUMN_NAME_REJECTED_COUNT + " INT, CONSTRAINT " + DOCUMENT_NAME + "_pk PRIMARY KEY (" + COLUMN_NAME_ID + "))";

        public int Id { get; set; }
    }
}