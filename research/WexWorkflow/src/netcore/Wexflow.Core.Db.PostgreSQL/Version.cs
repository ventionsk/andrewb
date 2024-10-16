﻿namespace Wexflow.Core.Db.PostgreSQL
{
    public class Version : Core.Db.Version
    {
        public const string COLUMN_NAME_ID = "ID";
        public const string COLUMN_NAME_RECORD_ID = "RECORD_ID";
        public const string COLUMN_NAME_FILE_PATH = "FILE_PATH";
        public const string COLUMN_NAME_CREATED_ON = "CREATED_ON";

        public const string TABLE_STRUCT = "(" + COLUMN_NAME_ID + " SERIAL PRIMARY KEY, "
                                                        + COLUMN_NAME_RECORD_ID + " INT, "
                                                        + COLUMN_NAME_FILE_PATH + " VARCHAR, "
                                                        + COLUMN_NAME_CREATED_ON + " TIMESTAMP)";

        public int Id { get; set; }

        public override string GetDbId()
        {
            return Id.ToString();
        }
    }
}
