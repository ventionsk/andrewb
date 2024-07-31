﻿namespace Wexflow.Core.Db.MariaDB
{
    public class Record : Core.Db.Record
    {
        public const string COLUMN_NAME_ID = "ID";
        public const string COLUMN_NAME_NAME = "NAME";
        public const string COLUMN_NAME_DESCRIPTION = "DESCRIPTION";
        public const string COLUMN_NAME_APPROVED = "APPROVED";
        public const string COLUMN_NAME_START_DATE = "START_DATE";
        public const string COLUMN_NAME_END_DATE = "END_DATE";
        public const string COLUMN_NAME_COMMENTS = "COMMENTS";
        public const string COLUMN_NAME_MANAGER_COMMENTS = "MANAGER_COMMENTS";
        public const string COLUMN_NAME_CREATED_BY = "CREATED_BY";
        public const string COLUMN_NAME_CREATED_ON = "CREATED_ON";
        public const string COLUMN_NAME_MODIFIED_BY = "MODIFIED_BY";
        public const string COLUMN_NAME_MODIFIED_ON = "MODIFIED_ON";
        public const string COLUMN_NAME_ASSIGNED_TO = "ASSIGNED_TO";
        public const string COLUMN_NAME_ASSIGNED_ON = "ASSIGNED_ON";

        public const string TABLE_STRUCT = "(" + COLUMN_NAME_ID + " INT NOT NULL AUTO_INCREMENT, "
                                                        + COLUMN_NAME_NAME + " VARCHAR(512), "
                                                        + COLUMN_NAME_DESCRIPTION + " LONGTEXT, "
                                                        + COLUMN_NAME_APPROVED + " BIT, "
                                                        + COLUMN_NAME_START_DATE + " TIMESTAMP NULL DEFAULT NULL, "
                                                        + COLUMN_NAME_END_DATE + " TIMESTAMP NULL DEFAULT NULL, "
                                                        + COLUMN_NAME_COMMENTS + " LONGTEXT, "
                                                        + COLUMN_NAME_MANAGER_COMMENTS + " LONGTEXT, "
                                                        + COLUMN_NAME_CREATED_BY + " INT, "
                                                        + COLUMN_NAME_CREATED_ON + " TIMESTAMP, "
                                                        + COLUMN_NAME_MODIFIED_BY + " INT, "
                                                        + COLUMN_NAME_MODIFIED_ON + " TIMESTAMP NULL DEFAULT NULL, "
                                                        + COLUMN_NAME_ASSIGNED_TO + " INT, "
                                                        + COLUMN_NAME_ASSIGNED_ON + " TIMESTAMP NULL DEFAULT NULL, CONSTRAINT " + DOCUMENT_NAME + "_pk PRIMARY KEY (" + COLUMN_NAME_ID + "))";

        public int Id { get; set; }

        public override string GetDbId()
        {
            return Id.ToString();
        }
    }
}
