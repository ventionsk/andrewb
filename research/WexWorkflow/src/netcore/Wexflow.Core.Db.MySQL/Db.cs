﻿using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Text;

namespace Wexflow.Core.Db.MySQL
{
    public sealed class Db : Core.Db.Db
    {
        private static readonly object Padlock = new();
        private const string DATE_TIME_FORMAT = "yyyy-MM-dd HH:mm:ss.fff";

        private static string _connectionString;

        public Db(string connectionString) : base(connectionString)
        {
            _connectionString = connectionString;

            var server = string.Empty;
            var uid = string.Empty;
            var pwd = string.Empty;
            var database = string.Empty;
            var port = 3306;

            var connectionStringParts = ConnectionString.Split(';');

            foreach (var part in connectionStringParts)
            {
                if (!string.IsNullOrEmpty(part.Trim()))
                {
                    var connPart = part.TrimStart(' ').TrimEnd(' ');
                    if (connPart.StartsWith("Server="))
                    {
                        server = connPart.Replace("Server=", string.Empty);
                    }
                    else if (connPart.StartsWith("Uid="))
                    {
                        uid = connPart.Replace("Uid=", string.Empty);
                    }
                    else if (connPart.StartsWith("Pwd="))
                    {
                        pwd = connPart.Replace("Pwd=", string.Empty);
                    }
                    else if (connPart.StartsWith("Database="))
                    {
                        database = connPart.Replace("Database=", string.Empty);
                    }
                    else if (connPart.StartsWith("Port="))
                    {
                        port = int.Parse(connPart.Replace("Port=", string.Empty));
                    }
                }
            }

            Helper helper = new(connectionString);
            Helper.CreateDatabaseIfNotExists(server, uid, pwd, database, port);
            helper.CreateTableIfNotExists(Core.Db.Entry.DOCUMENT_NAME, Entry.TABLE_STRUCT);
            helper.CreateTableIfNotExists(Core.Db.HistoryEntry.DOCUMENT_NAME, HistoryEntry.TABLE_STRUCT);
            helper.CreateTableIfNotExists(Core.Db.StatusCount.DOCUMENT_NAME, StatusCount.TABLE_STRUCT);
            helper.CreateTableIfNotExists(Core.Db.User.DOCUMENT_NAME, User.TABLE_STRUCT);
            helper.CreateTableIfNotExists(Core.Db.UserWorkflow.DOCUMENT_NAME, UserWorkflow.TABLE_STRUCT);
            helper.CreateTableIfNotExists(Core.Db.Workflow.DOCUMENT_NAME, Workflow.TABLE_STRUCT);
            helper.CreateTableIfNotExists(Core.Db.Version.DOCUMENT_NAME, Version.TABLE_STRUCT);
            helper.CreateTableIfNotExists(Core.Db.Record.DOCUMENT_NAME, Record.TABLE_STRUCT);
            helper.CreateTableIfNotExists(Core.Db.Notification.DOCUMENT_NAME, Notification.TABLE_STRUCT);
            helper.CreateTableIfNotExists(Core.Db.Approver.DOCUMENT_NAME, Approver.TABLE_STRUCT);
        }

        public override void Init()
        {
            // StatusCount
            ClearStatusCount();

            StatusCount statusCount = new()
            {
                PendingCount = 0,
                RunningCount = 0,
                DoneCount = 0,
                FailedCount = 0,
                WarningCount = 0,
                DisabledCount = 0,
                StoppedCount = 0
            };

            using (MySqlConnection conn = new(_connectionString))
            {
                conn.Open();

                using MySqlCommand command = new("INSERT INTO " + Core.Db.StatusCount.DOCUMENT_NAME + "("
                    + StatusCount.COLUMN_NAME_PENDING_COUNT + ", "
                    + StatusCount.COLUMN_NAME_RUNNING_COUNT + ", "
                    + StatusCount.COLUMN_NAME_DONE_COUNT + ", "
                    + StatusCount.COLUMN_NAME_FAILED_COUNT + ", "
                    + StatusCount.COLUMN_NAME_WARNING_COUNT + ", "
                    + StatusCount.COLUMN_NAME_DISABLED_COUNT + ", "
                    + StatusCount.COLUMN_NAME_STOPPED_COUNT + ", "
                    + StatusCount.COLUMN_NAME_REJECTED_COUNT + ") VALUES("
                    + statusCount.PendingCount + ", "
                    + statusCount.RunningCount + ", "
                    + statusCount.DoneCount + ", "
                    + statusCount.FailedCount + ", "
                    + statusCount.WarningCount + ", "
                    + statusCount.DisabledCount + ", "
                    + statusCount.StoppedCount + ", "
                    + statusCount.RejectedCount + ");"
                    , conn);
                _ = command.ExecuteNonQuery();
            }

            // Entries
            ClearEntries();

            // Insert default user if necessary
            using (MySqlConnection conn = new(_connectionString))
            {
                conn.Open();

                using MySqlCommand command = new("SELECT COUNT(*) FROM " + Core.Db.User.DOCUMENT_NAME + ";", conn);
                var usersCount = (long)command.ExecuteScalar()!;

                if (usersCount == 0)
                {
                    InsertDefaultUser();
                }
            }
        }

        public override bool CheckUserWorkflow(string userId, string workflowId)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT COUNT(*) FROM " + Core.Db.UserWorkflow.DOCUMENT_NAME
                    + " WHERE " + UserWorkflow.COLUMN_NAME_USER_ID + "=" + int.Parse(userId)
                    + " AND " + UserWorkflow.COLUMN_NAME_WORKFLOW_ID + "=" + int.Parse(workflowId)
                    + ";", conn);
                var count = (long)command.ExecuteScalar()!;

                return count > 0;
            }
        }

        public override void ClearEntries()
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("DELETE FROM " + Core.Db.Entry.DOCUMENT_NAME + ";", conn);
                _ = command.ExecuteNonQuery();
            }
        }

        public override void ClearStatusCount()
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("DELETE FROM " + Core.Db.StatusCount.DOCUMENT_NAME + ";", conn);
                _ = command.ExecuteNonQuery();
            }
        }

        public override void DeleteUser(string username, string password)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("DELETE FROM " + Core.Db.User.DOCUMENT_NAME
                    + " WHERE " + User.COLUMN_NAME_USERNAME + " = '" + username + "'"
                    + " AND " + User.COLUMN_NAME_PASSWORD + " = '" + password + "'"
                    + ";", conn);
                _ = command.ExecuteNonQuery();
            }
        }

        public override void DeleteUserWorkflowRelationsByUserId(string userId)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("DELETE FROM " + Core.Db.UserWorkflow.DOCUMENT_NAME
                    + " WHERE " + UserWorkflow.COLUMN_NAME_USER_ID + " = " + int.Parse(userId) + ";", conn);
                _ = command.ExecuteNonQuery();
            }
        }

        public override void DeleteUserWorkflowRelationsByWorkflowId(string workflowDbId)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("DELETE FROM " + Core.Db.UserWorkflow.DOCUMENT_NAME
                    + " WHERE " + UserWorkflow.COLUMN_NAME_WORKFLOW_ID + " = " + int.Parse(workflowDbId) + ";", conn);
                _ = command.ExecuteNonQuery();
            }
        }

        public override void DeleteWorkflow(string id)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("DELETE FROM " + Core.Db.Workflow.DOCUMENT_NAME
                    + " WHERE " + Workflow.COLUMN_NAME_ID + " = " + int.Parse(id) + ";", conn);
                _ = command.ExecuteNonQuery();
            }
        }

        public override void DeleteWorkflows(string[] ids)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                StringBuilder builder = new("(");

                for (var i = 0; i < ids.Length; i++)
                {
                    var id = ids[i];
                    _ = builder.Append(id);
                    _ = i < ids.Length - 1 ? builder.Append(", ") : builder.Append(')');
                }

                using MySqlCommand command = new("DELETE FROM " + Core.Db.Workflow.DOCUMENT_NAME
                    + " WHERE " + Workflow.COLUMN_NAME_ID + " IN " + builder + ";", conn);
                _ = command.ExecuteNonQuery();
            }
        }

        public override IEnumerable<Core.Db.User> GetAdministrators(string keyword, UserOrderBy uo)
        {
            lock (Padlock)
            {
                List<User> admins = new();

                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT " + User.COLUMN_NAME_ID + ", "
                                                 + User.COLUMN_NAME_USERNAME + ", "
                                                 + User.COLUMN_NAME_PASSWORD + ", "
                                                 + User.COLUMN_NAME_EMAIL + ", "
                                                 + User.COLUMN_NAME_USER_PROFILE + ", "
                                                 + User.COLUMN_NAME_CREATED_ON + ", "
                                                 + User.COLUMN_NAME_MODIFIED_ON
                                                 + " FROM " + Core.Db.User.DOCUMENT_NAME
                                                 + " WHERE " + "(LOWER(" + User.COLUMN_NAME_USERNAME + ")" + " LIKE '%" + (keyword ?? "").Replace("'", "''").Replace("\\", "\\\\").ToLower() + "%'"
                                                 + " AND " + User.COLUMN_NAME_USER_PROFILE + " = " + (int)UserProfile.Administrator + ")"
                                                 + " ORDER BY " + User.COLUMN_NAME_USERNAME + (uo == UserOrderBy.UsernameAscending ? " ASC" : " DESC")
                                                 + ";", conn);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    User admin = new()
                    {
                        Id = (int)reader[User.COLUMN_NAME_ID],
                        Username = (string)reader[User.COLUMN_NAME_USERNAME],
                        Password = (string)reader[User.COLUMN_NAME_PASSWORD],
                        Email = (string)reader[User.COLUMN_NAME_EMAIL],
                        UserProfile = (UserProfile)(int)reader[User.COLUMN_NAME_USER_PROFILE],
                        CreatedOn = (DateTime)reader[User.COLUMN_NAME_CREATED_ON],
                        ModifiedOn = reader[User.COLUMN_NAME_MODIFIED_ON] == DBNull.Value ? DateTime.MinValue : (DateTime)reader[User.COLUMN_NAME_MODIFIED_ON]
                    };

                    admins.Add(admin);
                }

                return admins;
            }
        }

        public override IEnumerable<Core.Db.Entry> GetEntries()
        {
            lock (Padlock)
            {
                List<Entry> entries = new();

                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT "
                    + Entry.COLUMN_NAME_ID + ", "
                    + Entry.COLUMN_NAME_NAME + ", "
                    + Entry.COLUMN_NAME_DESCRIPTION + ", "
                    + Entry.COLUMN_NAME_LAUNCH_TYPE + ", "
                    + Entry.COLUMN_NAME_STATUS + ", "
                    + Entry.COLUMN_NAME_STATUS_DATE + ", "
                    + Entry.COLUMN_NAME_WORKFLOW_ID + ", "
                    + Entry.COLUMN_NAME_JOB_ID
                    + " FROM " + Core.Db.Entry.DOCUMENT_NAME + ";", conn);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    Entry entry = new()
                    {
                        Id = (int)reader[Entry.COLUMN_NAME_ID],
                        Name = (string)reader[Entry.COLUMN_NAME_NAME],
                        Description = (string)reader[Entry.COLUMN_NAME_DESCRIPTION],
                        LaunchType = (LaunchType)(int)reader[Entry.COLUMN_NAME_LAUNCH_TYPE],
                        Status = (Status)(int)reader[Entry.COLUMN_NAME_STATUS],
                        StatusDate = (DateTime)reader[Entry.COLUMN_NAME_STATUS_DATE],
                        WorkflowId = (int)reader[Entry.COLUMN_NAME_WORKFLOW_ID],
                        JobId = (string)reader[Entry.COLUMN_NAME_JOB_ID]
                    };

                    entries.Add(entry);
                }

                return entries;
            }
        }

        public override IEnumerable<Core.Db.Entry> GetEntries(string keyword, DateTime from, DateTime to, int page, int entriesCount, EntryOrderBy eo)
        {
            lock (Padlock)
            {
                List<Entry> entries = new();

                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                StringBuilder sqlBuilder = new("SELECT "
                    + Entry.COLUMN_NAME_ID + ", "
                    + Entry.COLUMN_NAME_NAME + ", "
                    + Entry.COLUMN_NAME_DESCRIPTION + ", "
                    + Entry.COLUMN_NAME_LAUNCH_TYPE + ", "
                    + Entry.COLUMN_NAME_STATUS + ", "
                    + Entry.COLUMN_NAME_STATUS_DATE + ", "
                    + Entry.COLUMN_NAME_WORKFLOW_ID + ", "
                    + Entry.COLUMN_NAME_JOB_ID
                    + " FROM " + Core.Db.Entry.DOCUMENT_NAME
                    + " WHERE " + "(LOWER(" + Entry.COLUMN_NAME_NAME + ") LIKE '%" + (keyword ?? "").Replace("'", "''").Replace("\\", "\\\\").ToLower() + "%'"
                    + " OR " + "LOWER(" + Entry.COLUMN_NAME_DESCRIPTION + ") LIKE '%" + (keyword ?? "").Replace("'", "''").Replace("\\", "\\\\").ToLower() + "%')"
                    + " AND (" + Entry.COLUMN_NAME_STATUS_DATE + " BETWEEN '" + from.ToString(DATE_TIME_FORMAT) + "' AND '" + to.ToString(DATE_TIME_FORMAT) + "')"
                    + " ORDER BY ");

                switch (eo)
                {
                    case EntryOrderBy.StatusDateAscending:

                        _ = sqlBuilder.Append(Entry.COLUMN_NAME_STATUS_DATE).Append(" ASC");
                        break;

                    case EntryOrderBy.StatusDateDescending:

                        _ = sqlBuilder.Append(Entry.COLUMN_NAME_STATUS_DATE).Append(" DESC");
                        break;

                    case EntryOrderBy.WorkflowIdAscending:

                        _ = sqlBuilder.Append(Entry.COLUMN_NAME_WORKFLOW_ID).Append(" ASC");
                        break;

                    case EntryOrderBy.WorkflowIdDescending:

                        _ = sqlBuilder.Append(Entry.COLUMN_NAME_WORKFLOW_ID).Append(" DESC");
                        break;

                    case EntryOrderBy.NameAscending:

                        _ = sqlBuilder.Append(Entry.COLUMN_NAME_NAME).Append(" ASC");
                        break;

                    case EntryOrderBy.NameDescending:

                        _ = sqlBuilder.Append(Entry.COLUMN_NAME_NAME).Append(" DESC");
                        break;

                    case EntryOrderBy.LaunchTypeAscending:

                        _ = sqlBuilder.Append(Entry.COLUMN_NAME_LAUNCH_TYPE).Append(" ASC");
                        break;

                    case EntryOrderBy.LaunchTypeDescending:

                        _ = sqlBuilder.Append(Entry.COLUMN_NAME_LAUNCH_TYPE).Append(" DESC");
                        break;

                    case EntryOrderBy.DescriptionAscending:

                        _ = sqlBuilder.Append(Entry.COLUMN_NAME_DESCRIPTION).Append(" ASC");
                        break;

                    case EntryOrderBy.DescriptionDescending:

                        _ = sqlBuilder.Append(Entry.COLUMN_NAME_DESCRIPTION).Append(" DESC");
                        break;

                    case EntryOrderBy.StatusAscending:

                        _ = sqlBuilder.Append(Entry.COLUMN_NAME_STATUS).Append(" ASC");
                        break;

                    case EntryOrderBy.StatusDescending:

                        _ = sqlBuilder.Append(Entry.COLUMN_NAME_STATUS).Append(" DESC");
                        break;
                }

                _ = sqlBuilder.Append(" LIMIT ").Append(entriesCount).Append(" OFFSET ").Append((page - 1) * entriesCount).Append(';');

                using MySqlCommand command = new(sqlBuilder.ToString(), conn);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    Entry entry = new()
                    {
                        Id = (int)reader[Entry.COLUMN_NAME_ID],
                        Name = (string)reader[Entry.COLUMN_NAME_NAME],
                        Description = (string)reader[Entry.COLUMN_NAME_DESCRIPTION],
                        LaunchType = (LaunchType)(int)reader[Entry.COLUMN_NAME_LAUNCH_TYPE],
                        Status = (Status)(int)reader[Entry.COLUMN_NAME_STATUS],
                        StatusDate = (DateTime)reader[Entry.COLUMN_NAME_STATUS_DATE],
                        WorkflowId = (int)reader[Entry.COLUMN_NAME_WORKFLOW_ID],
                        JobId = (string)reader[Entry.COLUMN_NAME_JOB_ID]
                    };

                    entries.Add(entry);
                }

                return entries;
            }
        }

        public override long GetEntriesCount(string keyword, DateTime from, DateTime to)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT COUNT(*)"
                    + " FROM " + Core.Db.Entry.DOCUMENT_NAME
                    + " WHERE " + "(LOWER(" + Entry.COLUMN_NAME_NAME + ") LIKE '%" + (keyword ?? "").Replace("'", "''").Replace("\\", "\\\\").ToLower() + "%'"
                    + " OR " + "LOWER(" + Entry.COLUMN_NAME_DESCRIPTION + ") LIKE '%" + (keyword ?? "").Replace("'", "''").Replace("\\", "\\\\").ToLower() + "%')"
                    + " AND (" + Entry.COLUMN_NAME_STATUS_DATE + " BETWEEN '" + from.ToString(DATE_TIME_FORMAT) + "' AND '" + to.ToString(DATE_TIME_FORMAT) + "');", conn);

                var count = (long)command.ExecuteScalar()!;

                return count;
            }
        }

        public override Core.Db.Entry GetEntry(int workflowId)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT "
                    + Entry.COLUMN_NAME_ID + ", "
                    + Entry.COLUMN_NAME_NAME + ", "
                    + Entry.COLUMN_NAME_DESCRIPTION + ", "
                    + Entry.COLUMN_NAME_LAUNCH_TYPE + ", "
                    + Entry.COLUMN_NAME_STATUS + ", "
                    + Entry.COLUMN_NAME_STATUS_DATE + ", "
                    + Entry.COLUMN_NAME_WORKFLOW_ID + ", "
                    + Entry.COLUMN_NAME_JOB_ID
                    + " FROM " + Core.Db.Entry.DOCUMENT_NAME
                    + " WHERE " + Entry.COLUMN_NAME_WORKFLOW_ID + " = " + workflowId + ";", conn);

                using var reader = command.ExecuteReader();

                if (reader.Read())
                {
                    Entry entry = new()
                    {
                        Id = (int)reader[Entry.COLUMN_NAME_ID],
                        Name = (string)reader[Entry.COLUMN_NAME_NAME],
                        Description = (string)reader[Entry.COLUMN_NAME_DESCRIPTION],
                        LaunchType = (LaunchType)(int)reader[Entry.COLUMN_NAME_LAUNCH_TYPE],
                        Status = (Status)(int)reader[Entry.COLUMN_NAME_STATUS],
                        StatusDate = (DateTime)reader[Entry.COLUMN_NAME_STATUS_DATE],
                        WorkflowId = (int)reader[Entry.COLUMN_NAME_WORKFLOW_ID],
                        JobId = (string)reader[Entry.COLUMN_NAME_JOB_ID]
                    };

                    return entry;
                }

                return null;
            }
        }

        public override Core.Db.Entry GetEntry(int workflowId, Guid jobId)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT "
                    + Entry.COLUMN_NAME_ID + ", "
                    + Entry.COLUMN_NAME_NAME + ", "
                    + Entry.COLUMN_NAME_DESCRIPTION + ", "
                    + Entry.COLUMN_NAME_LAUNCH_TYPE + ", "
                    + Entry.COLUMN_NAME_STATUS + ", "
                    + Entry.COLUMN_NAME_STATUS_DATE + ", "
                    + Entry.COLUMN_NAME_WORKFLOW_ID + ", "
                    + Entry.COLUMN_NAME_JOB_ID
                    + " FROM " + Core.Db.Entry.DOCUMENT_NAME
                    + " WHERE (" + Entry.COLUMN_NAME_WORKFLOW_ID + " = " + workflowId
                    + " AND " + Entry.COLUMN_NAME_JOB_ID + " = '" + jobId + "');", conn);

                using var reader = command.ExecuteReader();

                if (reader.Read())
                {
                    Entry entry = new()
                    {
                        Id = (int)reader[Entry.COLUMN_NAME_ID],
                        Name = (string)reader[Entry.COLUMN_NAME_NAME],
                        Description = (string)reader[Entry.COLUMN_NAME_DESCRIPTION],
                        LaunchType = (LaunchType)(int)reader[Entry.COLUMN_NAME_LAUNCH_TYPE],
                        Status = (Status)(int)reader[Entry.COLUMN_NAME_STATUS],
                        StatusDate = (DateTime)reader[Entry.COLUMN_NAME_STATUS_DATE],
                        WorkflowId = (int)reader[Entry.COLUMN_NAME_WORKFLOW_ID],
                        JobId = (string)reader[Entry.COLUMN_NAME_JOB_ID]
                    };

                    return entry;
                }

                return null;
            }
        }

        public override DateTime GetEntryStatusDateMax()
        {
            lock (Padlock)
            {
                using (MySqlConnection conn = new(_connectionString))
                {
                    conn.Open();

                    using MySqlCommand command = new("SELECT " + Entry.COLUMN_NAME_STATUS_DATE
                        + " FROM " + Core.Db.Entry.DOCUMENT_NAME
                        + " ORDER BY " + Entry.COLUMN_NAME_STATUS_DATE + " DESC LIMIT 1;", conn);

                    using var reader = command.ExecuteReader();

                    if (reader.Read())
                    {
                        var statusDate = (DateTime)reader[Entry.COLUMN_NAME_STATUS_DATE];

                        return statusDate;
                    }
                }

                return DateTime.Now;
            }
        }

        public override DateTime GetEntryStatusDateMin()
        {
            lock (Padlock)
            {
                using (MySqlConnection conn = new(_connectionString))
                {
                    conn.Open();

                    using MySqlCommand command = new("SELECT " + Entry.COLUMN_NAME_STATUS_DATE
                        + " FROM " + Core.Db.Entry.DOCUMENT_NAME
                        + " ORDER BY " + Entry.COLUMN_NAME_STATUS_DATE + " ASC LIMIT 1;", conn);

                    using var reader = command.ExecuteReader();

                    if (reader.Read())
                    {
                        var statusDate = (DateTime)reader[Entry.COLUMN_NAME_STATUS_DATE];

                        return statusDate;
                    }
                }

                return DateTime.Now;
            }
        }

        public override IEnumerable<Core.Db.HistoryEntry> GetHistoryEntries()
        {
            lock (Padlock)
            {
                List<HistoryEntry> entries = new();

                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT "
                    + HistoryEntry.COLUMN_NAME_ID + ", "
                    + HistoryEntry.COLUMN_NAME_NAME + ", "
                    + HistoryEntry.COLUMN_NAME_DESCRIPTION + ", "
                    + HistoryEntry.COLUMN_NAME_LAUNCH_TYPE + ", "
                    + HistoryEntry.COLUMN_NAME_STATUS + ", "
                    + HistoryEntry.COLUMN_NAME_STATUS_DATE + ", "
                    + HistoryEntry.COLUMN_NAME_WORKFLOW_ID
                    + " FROM " + Core.Db.HistoryEntry.DOCUMENT_NAME + ";", conn);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    HistoryEntry entry = new()
                    {
                        Id = (int)reader[HistoryEntry.COLUMN_NAME_ID],
                        Name = (string)reader[HistoryEntry.COLUMN_NAME_NAME],
                        Description = (string)reader[HistoryEntry.COLUMN_NAME_DESCRIPTION],
                        LaunchType = (LaunchType)(int)reader[HistoryEntry.COLUMN_NAME_LAUNCH_TYPE],
                        Status = (Status)(int)reader[HistoryEntry.COLUMN_NAME_STATUS],
                        StatusDate = (DateTime)reader[HistoryEntry.COLUMN_NAME_STATUS_DATE],
                        WorkflowId = (int)reader[HistoryEntry.COLUMN_NAME_WORKFLOW_ID]
                    };

                    entries.Add(entry);
                }

                return entries;
            }
        }

        public override IEnumerable<Core.Db.HistoryEntry> GetHistoryEntries(string keyword)
        {
            lock (Padlock)
            {
                List<HistoryEntry> entries = new();

                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT "
                    + HistoryEntry.COLUMN_NAME_ID + ", "
                    + HistoryEntry.COLUMN_NAME_NAME + ", "
                    + HistoryEntry.COLUMN_NAME_DESCRIPTION + ", "
                    + HistoryEntry.COLUMN_NAME_LAUNCH_TYPE + ", "
                    + HistoryEntry.COLUMN_NAME_STATUS + ", "
                    + HistoryEntry.COLUMN_NAME_STATUS_DATE + ", "
                    + HistoryEntry.COLUMN_NAME_WORKFLOW_ID
                    + " FROM " + Core.Db.HistoryEntry.DOCUMENT_NAME
                    + " WHERE " + "LOWER(" + HistoryEntry.COLUMN_NAME_NAME + ") LIKE '%" + (keyword ?? "").Replace("'", "''").Replace("\\", "\\\\").ToLower() + "%'"
                    + " OR " + "LOWER(" + HistoryEntry.COLUMN_NAME_DESCRIPTION + ") LIKE '%" + (keyword ?? "").Replace("'", "''").Replace("\\", "\\\\").ToLower() + "%';", conn);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    HistoryEntry entry = new()
                    {
                        Id = (int)reader[HistoryEntry.COLUMN_NAME_ID],
                        Name = (string)reader[HistoryEntry.COLUMN_NAME_NAME],
                        Description = (string)reader[HistoryEntry.COLUMN_NAME_DESCRIPTION],
                        LaunchType = (LaunchType)(int)reader[HistoryEntry.COLUMN_NAME_LAUNCH_TYPE],
                        Status = (Status)(int)reader[HistoryEntry.COLUMN_NAME_STATUS],
                        StatusDate = (DateTime)reader[HistoryEntry.COLUMN_NAME_STATUS_DATE],
                        WorkflowId = (int)reader[HistoryEntry.COLUMN_NAME_WORKFLOW_ID]
                    };

                    entries.Add(entry);
                }

                return entries;
            }
        }

        public override IEnumerable<Core.Db.HistoryEntry> GetHistoryEntries(string keyword, int page, int entriesCount)
        {
            lock (Padlock)
            {
                List<HistoryEntry> entries = new();

                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT "
                    + HistoryEntry.COLUMN_NAME_ID + ", "
                    + HistoryEntry.COLUMN_NAME_NAME + ", "
                    + HistoryEntry.COLUMN_NAME_DESCRIPTION + ", "
                    + HistoryEntry.COLUMN_NAME_LAUNCH_TYPE + ", "
                    + HistoryEntry.COLUMN_NAME_STATUS + ", "
                    + HistoryEntry.COLUMN_NAME_STATUS_DATE + ", "
                    + HistoryEntry.COLUMN_NAME_WORKFLOW_ID
                    + " FROM " + Core.Db.HistoryEntry.DOCUMENT_NAME
                    + " WHERE " + "LOWER(" + HistoryEntry.COLUMN_NAME_NAME + ") LIKE '%" + (keyword ?? "").Replace("'", "''").Replace("\\", "\\\\").ToLower() + "%'"
                    + " OR " + "LOWER(" + HistoryEntry.COLUMN_NAME_DESCRIPTION + ") LIKE '%" + (keyword ?? "").Replace("'", "''").Replace("\\", "\\\\").ToLower() + "%'"
                    + " LIMIT " + entriesCount + " OFFSET " + ((page - 1) * entriesCount) + ";"
                    , conn);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    HistoryEntry entry = new()
                    {
                        Id = (int)reader[HistoryEntry.COLUMN_NAME_ID],
                        Name = (string)reader[HistoryEntry.COLUMN_NAME_NAME],
                        Description = (string)reader[HistoryEntry.COLUMN_NAME_DESCRIPTION],
                        LaunchType = (LaunchType)(int)reader[HistoryEntry.COLUMN_NAME_LAUNCH_TYPE],
                        Status = (Status)(int)reader[HistoryEntry.COLUMN_NAME_STATUS],
                        StatusDate = (DateTime)reader[HistoryEntry.COLUMN_NAME_STATUS_DATE],
                        WorkflowId = (int)reader[HistoryEntry.COLUMN_NAME_WORKFLOW_ID]
                    };

                    entries.Add(entry);
                }

                return entries;
            }
        }

        public override IEnumerable<Core.Db.HistoryEntry> GetHistoryEntries(string keyword, DateTime from, DateTime to, int page, int entriesCount, EntryOrderBy heo)
        {
            lock (Padlock)
            {
                List<HistoryEntry> entries = new();

                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                StringBuilder sqlBuilder = new("SELECT "
                    + HistoryEntry.COLUMN_NAME_ID + ", "
                    + HistoryEntry.COLUMN_NAME_NAME + ", "
                    + HistoryEntry.COLUMN_NAME_DESCRIPTION + ", "
                    + HistoryEntry.COLUMN_NAME_LAUNCH_TYPE + ", "
                    + HistoryEntry.COLUMN_NAME_STATUS + ", "
                    + HistoryEntry.COLUMN_NAME_STATUS_DATE + ", "
                    + HistoryEntry.COLUMN_NAME_WORKFLOW_ID
                    + " FROM " + Core.Db.HistoryEntry.DOCUMENT_NAME
                    + " WHERE " + "(LOWER(" + HistoryEntry.COLUMN_NAME_NAME + ") LIKE '%" + (keyword ?? "").Replace("'", "''").Replace("\\", "\\\\").ToLower() + "%'"
                    + " OR " + "LOWER(" + HistoryEntry.COLUMN_NAME_DESCRIPTION + ") LIKE '%" + (keyword ?? "").Replace("'", "''").Replace("\\", "\\\\").ToLower() + "%')"
                    + " AND (" + HistoryEntry.COLUMN_NAME_STATUS_DATE + " BETWEEN '" + from.ToString(DATE_TIME_FORMAT) + "' AND '" + to.ToString(DATE_TIME_FORMAT) + "')"
                    + " ORDER BY ");

                switch (heo)
                {
                    case EntryOrderBy.StatusDateAscending:

                        _ = sqlBuilder.Append(HistoryEntry.COLUMN_NAME_STATUS_DATE).Append(" ASC");
                        break;

                    case EntryOrderBy.StatusDateDescending:

                        _ = sqlBuilder.Append(HistoryEntry.COLUMN_NAME_STATUS_DATE).Append(" DESC");
                        break;

                    case EntryOrderBy.WorkflowIdAscending:

                        _ = sqlBuilder.Append(HistoryEntry.COLUMN_NAME_WORKFLOW_ID).Append(" ASC");
                        break;

                    case EntryOrderBy.WorkflowIdDescending:

                        _ = sqlBuilder.Append(HistoryEntry.COLUMN_NAME_WORKFLOW_ID).Append(" DESC");
                        break;

                    case EntryOrderBy.NameAscending:

                        _ = sqlBuilder.Append(HistoryEntry.COLUMN_NAME_NAME).Append(" ASC");
                        break;

                    case EntryOrderBy.NameDescending:

                        _ = sqlBuilder.Append(HistoryEntry.COLUMN_NAME_NAME).Append(" DESC");
                        break;

                    case EntryOrderBy.LaunchTypeAscending:

                        _ = sqlBuilder.Append(HistoryEntry.COLUMN_NAME_LAUNCH_TYPE).Append(" ASC");
                        break;

                    case EntryOrderBy.LaunchTypeDescending:

                        _ = sqlBuilder.Append(HistoryEntry.COLUMN_NAME_LAUNCH_TYPE).Append(" DESC");
                        break;

                    case EntryOrderBy.DescriptionAscending:

                        _ = sqlBuilder.Append(HistoryEntry.COLUMN_NAME_DESCRIPTION).Append(" ASC");
                        break;

                    case EntryOrderBy.DescriptionDescending:

                        _ = sqlBuilder.Append(HistoryEntry.COLUMN_NAME_DESCRIPTION).Append(" DESC");
                        break;

                    case EntryOrderBy.StatusAscending:

                        _ = sqlBuilder.Append(HistoryEntry.COLUMN_NAME_STATUS).Append(" ASC");
                        break;

                    case EntryOrderBy.StatusDescending:

                        _ = sqlBuilder.Append(HistoryEntry.COLUMN_NAME_STATUS).Append(" DESC");
                        break;
                }

                _ = sqlBuilder.Append(" LIMIT ").Append(entriesCount).Append(" OFFSET ").Append((page - 1) * entriesCount).Append(';');

                using MySqlCommand command = new(sqlBuilder.ToString(), conn);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    HistoryEntry entry = new()
                    {
                        Id = (int)reader[HistoryEntry.COLUMN_NAME_ID],
                        Name = (string)reader[HistoryEntry.COLUMN_NAME_NAME],
                        Description = (string)reader[HistoryEntry.COLUMN_NAME_DESCRIPTION],
                        LaunchType = (LaunchType)(int)reader[HistoryEntry.COLUMN_NAME_LAUNCH_TYPE],
                        Status = (Status)(int)reader[HistoryEntry.COLUMN_NAME_STATUS],
                        StatusDate = (DateTime)reader[HistoryEntry.COLUMN_NAME_STATUS_DATE],
                        WorkflowId = (int)reader[HistoryEntry.COLUMN_NAME_WORKFLOW_ID]
                    };

                    entries.Add(entry);
                }

                return entries;
            }
        }

        public override long GetHistoryEntriesCount(string keyword)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT COUNT(*)"
                    + " FROM " + Core.Db.HistoryEntry.DOCUMENT_NAME
                    + " WHERE " + "LOWER(" + HistoryEntry.COLUMN_NAME_NAME + ") LIKE '%" + (keyword ?? "").Replace("'", "''").Replace("\\", "\\\\").ToLower() + "%'"
                    + " OR " + "LOWER(" + HistoryEntry.COLUMN_NAME_DESCRIPTION + ") LIKE '%" + (keyword ?? "").Replace("'", "''").Replace("\\", "\\\\").ToLower() + "%';", conn);

                var count = (long)command.ExecuteScalar()!;

                return count;
            }
        }

        public override long GetHistoryEntriesCount(string keyword, DateTime from, DateTime to)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT COUNT(*)"
                    + " FROM " + Core.Db.HistoryEntry.DOCUMENT_NAME
                    + " WHERE " + "(LOWER(" + HistoryEntry.COLUMN_NAME_NAME + ") LIKE '%" + (keyword ?? "").Replace("'", "''").Replace("\\", "\\\\").ToLower() + "%'"
                    + " OR " + "LOWER(" + HistoryEntry.COLUMN_NAME_DESCRIPTION + ") LIKE '%" + (keyword ?? "").Replace("'", "''").Replace("\\", "\\\\").ToLower() + "%')"
                    + " AND (" + HistoryEntry.COLUMN_NAME_STATUS_DATE + " BETWEEN '" + from.ToString(DATE_TIME_FORMAT) + "' AND '" + to.ToString(DATE_TIME_FORMAT) + "');", conn);

                var count = (long)command.ExecuteScalar()!;

                return count;
            }
        }

        public override DateTime GetHistoryEntryStatusDateMax()
        {
            lock (Padlock)
            {
                using (MySqlConnection conn = new(_connectionString))
                {
                    conn.Open();

                    using MySqlCommand command = new("SELECT " + HistoryEntry.COLUMN_NAME_STATUS_DATE
                        + " FROM " + Core.Db.HistoryEntry.DOCUMENT_NAME
                        + " ORDER BY " + HistoryEntry.COLUMN_NAME_STATUS_DATE + " DESC LIMIT 1;", conn);

                    using var reader = command.ExecuteReader();

                    if (reader.Read())
                    {
                        var statusDate = (DateTime)reader[HistoryEntry.COLUMN_NAME_STATUS_DATE];

                        return statusDate;
                    }
                }

                return DateTime.Now;
            }
        }

        public override DateTime GetHistoryEntryStatusDateMin()
        {
            lock (Padlock)
            {
                using (MySqlConnection conn = new(_connectionString))
                {
                    conn.Open();

                    using MySqlCommand command = new("SELECT " + HistoryEntry.COLUMN_NAME_STATUS_DATE
                        + " FROM " + Core.Db.HistoryEntry.DOCUMENT_NAME
                        + " ORDER BY " + HistoryEntry.COLUMN_NAME_STATUS_DATE + " ASC LIMIT 1;", conn);

                    using var reader = command.ExecuteReader();

                    if (reader.Read())
                    {
                        var statusDate = (DateTime)reader[HistoryEntry.COLUMN_NAME_STATUS_DATE];

                        return statusDate;
                    }
                }

                return DateTime.Now;
            }
        }

        public override string GetPassword(string username)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT " + User.COLUMN_NAME_PASSWORD
                    + " FROM " + Core.Db.User.DOCUMENT_NAME
                    + " WHERE " + User.COLUMN_NAME_USERNAME + " = '" + username + "'"
                    + ";", conn);

                using var reader = command.ExecuteReader();

                if (reader.Read())
                {
                    var password = (string)reader[User.COLUMN_NAME_PASSWORD];

                    return password;
                }

                return null;
            }
        }

        public override Core.Db.StatusCount GetStatusCount()
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT " + StatusCount.COLUMN_NAME_ID + ", "
                    + StatusCount.COLUMN_NAME_PENDING_COUNT + ", "
                    + StatusCount.COLUMN_NAME_RUNNING_COUNT + ", "
                    + StatusCount.COLUMN_NAME_DONE_COUNT + ", "
                    + StatusCount.COLUMN_NAME_FAILED_COUNT + ", "
                    + StatusCount.COLUMN_NAME_WARNING_COUNT + ", "
                    + StatusCount.COLUMN_NAME_DISABLED_COUNT + ", "
                    + StatusCount.COLUMN_NAME_STOPPED_COUNT + ", "
                    + StatusCount.COLUMN_NAME_REJECTED_COUNT
                    + " FROM " + Core.Db.StatusCount.DOCUMENT_NAME
                    + ";", conn);

                using var reader = command.ExecuteReader();

                if (reader.Read())
                {
                    StatusCount statusCount = new()
                    {
                        Id = (int)reader[StatusCount.COLUMN_NAME_ID],
                        PendingCount = (int)reader[StatusCount.COLUMN_NAME_PENDING_COUNT],
                        RunningCount = (int)reader[StatusCount.COLUMN_NAME_RUNNING_COUNT],
                        DoneCount = (int)reader[StatusCount.COLUMN_NAME_DONE_COUNT],
                        FailedCount = (int)reader[StatusCount.COLUMN_NAME_FAILED_COUNT],
                        WarningCount = (int)reader[StatusCount.COLUMN_NAME_WARNING_COUNT],
                        DisabledCount = (int)reader[StatusCount.COLUMN_NAME_DISABLED_COUNT],
                        StoppedCount = (int)reader[StatusCount.COLUMN_NAME_STOPPED_COUNT],
                        RejectedCount = (int)reader[StatusCount.COLUMN_NAME_REJECTED_COUNT]
                    };

                    return statusCount;
                }

                return null;
            }
        }

        public override Core.Db.User GetUser(string username)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT " + User.COLUMN_NAME_ID + ", "
                    + User.COLUMN_NAME_USERNAME + ", "
                    + User.COLUMN_NAME_PASSWORD + ", "
                    + User.COLUMN_NAME_EMAIL + ", "
                    + User.COLUMN_NAME_USER_PROFILE + ", "
                    + User.COLUMN_NAME_CREATED_ON + ", "
                    + User.COLUMN_NAME_MODIFIED_ON
                    + " FROM " + Core.Db.User.DOCUMENT_NAME
                    + " WHERE " + User.COLUMN_NAME_USERNAME + " = '" + (username ?? "").Replace("'", "''").Replace("\\", "\\\\") + "'"
                    + ";", conn);

                using var reader = command.ExecuteReader();

                if (reader.Read())
                {
                    User user = new()
                    {
                        Id = (int)reader[User.COLUMN_NAME_ID],
                        Username = (string)reader[User.COLUMN_NAME_USERNAME],
                        Password = (string)reader[User.COLUMN_NAME_PASSWORD],
                        Email = (string)reader[User.COLUMN_NAME_EMAIL],
                        UserProfile = (UserProfile)(int)reader[User.COLUMN_NAME_USER_PROFILE],
                        CreatedOn = (DateTime)reader[User.COLUMN_NAME_CREATED_ON],
                        ModifiedOn = reader[User.COLUMN_NAME_MODIFIED_ON] == DBNull.Value ? DateTime.MinValue : (DateTime)reader[User.COLUMN_NAME_MODIFIED_ON]
                    };

                    return user;
                }

                return null;
            }
        }

        public override Core.Db.User GetUserById(string userId)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT " + User.COLUMN_NAME_ID + ", "
                    + User.COLUMN_NAME_USERNAME + ", "
                    + User.COLUMN_NAME_PASSWORD + ", "
                    + User.COLUMN_NAME_EMAIL + ", "
                    + User.COLUMN_NAME_USER_PROFILE + ", "
                    + User.COLUMN_NAME_CREATED_ON + ", "
                    + User.COLUMN_NAME_MODIFIED_ON
                    + " FROM " + Core.Db.User.DOCUMENT_NAME
                    + " WHERE " + User.COLUMN_NAME_ID + " = '" + int.Parse(userId) + "'"
                    + ";", conn);

                using var reader = command.ExecuteReader();

                if (reader.Read())
                {
                    User user = new()
                    {
                        Id = (int)reader[User.COLUMN_NAME_ID],
                        Username = (string)reader[User.COLUMN_NAME_USERNAME],
                        Password = (string)reader[User.COLUMN_NAME_PASSWORD],
                        Email = (string)reader[User.COLUMN_NAME_EMAIL],
                        UserProfile = (UserProfile)(int)reader[User.COLUMN_NAME_USER_PROFILE],
                        CreatedOn = (DateTime)reader[User.COLUMN_NAME_CREATED_ON],
                        ModifiedOn = reader[User.COLUMN_NAME_MODIFIED_ON] == DBNull.Value ? DateTime.MinValue : (DateTime)reader[User.COLUMN_NAME_MODIFIED_ON]
                    };

                    return user;
                }

                return null;
            }
        }

        public override IEnumerable<Core.Db.User> GetUsers()
        {
            lock (Padlock)
            {
                List<User> users = new();

                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT " + User.COLUMN_NAME_ID + ", "
                                                 + User.COLUMN_NAME_USERNAME + ", "
                                                 + User.COLUMN_NAME_PASSWORD + ", "
                                                 + User.COLUMN_NAME_EMAIL + ", "
                                                 + User.COLUMN_NAME_USER_PROFILE + ", "
                                                 + User.COLUMN_NAME_CREATED_ON + ", "
                                                 + User.COLUMN_NAME_MODIFIED_ON
                                                 + " FROM " + Core.Db.User.DOCUMENT_NAME
                                                 + ";", conn);
                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    User user = new()
                    {
                        Id = (int)reader[User.COLUMN_NAME_ID],
                        Username = (string)reader[User.COLUMN_NAME_USERNAME],
                        Password = (string)reader[User.COLUMN_NAME_PASSWORD],
                        Email = (string)reader[User.COLUMN_NAME_EMAIL],
                        UserProfile = (UserProfile)(int)reader[User.COLUMN_NAME_USER_PROFILE],
                        CreatedOn = (DateTime)reader[User.COLUMN_NAME_CREATED_ON],
                        ModifiedOn = reader[User.COLUMN_NAME_MODIFIED_ON] == DBNull.Value ? DateTime.MinValue : (DateTime)reader[User.COLUMN_NAME_MODIFIED_ON]
                    };

                    users.Add(user);
                }

                return users;
            }
        }

        public override IEnumerable<Core.Db.User> GetUsers(string keyword, UserOrderBy uo)
        {
            lock (Padlock)
            {
                List<User> users = new();

                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT " + User.COLUMN_NAME_ID + ", "
                                                 + User.COLUMN_NAME_USERNAME + ", "
                                                 + User.COLUMN_NAME_PASSWORD + ", "
                                                 + User.COLUMN_NAME_EMAIL + ", "
                                                 + User.COLUMN_NAME_USER_PROFILE + ", "
                                                 + User.COLUMN_NAME_CREATED_ON + ", "
                                                 + User.COLUMN_NAME_MODIFIED_ON
                                                 + " FROM " + Core.Db.User.DOCUMENT_NAME
                                                 + " WHERE " + "LOWER(" + User.COLUMN_NAME_USERNAME + ")" + " LIKE '%" + (keyword ?? "").Replace("'", "''").Replace("\\", "\\\\").ToLower() + "%'"
                                                 + " ORDER BY " + User.COLUMN_NAME_USERNAME + (uo == UserOrderBy.UsernameAscending ? " ASC" : " DESC")
                                                 + ";", conn);

                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    User user = new()
                    {
                        Id = (int)reader[User.COLUMN_NAME_ID],
                        Username = (string)reader[User.COLUMN_NAME_USERNAME],
                        Password = (string)reader[User.COLUMN_NAME_PASSWORD],
                        Email = (string)reader[User.COLUMN_NAME_EMAIL],
                        UserProfile = (UserProfile)(int)reader[User.COLUMN_NAME_USER_PROFILE],
                        CreatedOn = (DateTime)reader[User.COLUMN_NAME_CREATED_ON],
                        ModifiedOn = reader[User.COLUMN_NAME_MODIFIED_ON] == DBNull.Value ? DateTime.MinValue : (DateTime)reader[User.COLUMN_NAME_MODIFIED_ON]
                    };

                    users.Add(user);
                }

                return users;
            }
        }

        public override IEnumerable<string> GetUserWorkflows(string userId)
        {
            lock (Padlock)
            {
                List<string> workflowIds = new();

                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT " + UserWorkflow.COLUMN_NAME_ID + ", "
                                                 + UserWorkflow.COLUMN_NAME_USER_ID + ", "
                                                 + UserWorkflow.COLUMN_NAME_WORKFLOW_ID
                                                 + " FROM " + Core.Db.UserWorkflow.DOCUMENT_NAME
                                                 + " WHERE " + UserWorkflow.COLUMN_NAME_USER_ID + " = " + int.Parse(userId)
                                                 + ";", conn);

                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    var workflowId = (int)reader[UserWorkflow.COLUMN_NAME_WORKFLOW_ID];

                    workflowIds.Add(workflowId.ToString());
                }

                return workflowIds;
            }
        }

        public override Core.Db.Workflow GetWorkflow(string id)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT " + Workflow.COLUMN_NAME_ID + ", "
                    + Workflow.COLUMN_NAME_XML
                    + " FROM " + Core.Db.Workflow.DOCUMENT_NAME
                    + " WHERE " + Workflow.COLUMN_NAME_ID + " = " + int.Parse(id) + ";", conn);

                using var reader = command.ExecuteReader();

                if (reader.Read())
                {
                    Workflow workflow = new()
                    {
                        Id = (int)reader[Workflow.COLUMN_NAME_ID],
                        Xml = (string)reader[Workflow.COLUMN_NAME_XML]
                    };

                    return workflow;
                }

                return null;
            }
        }

        public override IEnumerable<Core.Db.Workflow> GetWorkflows()
        {
            lock (Padlock)
            {
                List<Core.Db.Workflow> workflows = new();

                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT " + Workflow.COLUMN_NAME_ID + ", "
                                                 + Workflow.COLUMN_NAME_XML
                                                 + " FROM " + Core.Db.Workflow.DOCUMENT_NAME + ";", conn);
                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    Workflow workflow = new()
                    {
                        Id = (int)reader[Workflow.COLUMN_NAME_ID],
                        Xml = (string)reader[Workflow.COLUMN_NAME_XML]
                    };

                    workflows.Add(workflow);
                }

                return workflows;
            }
        }

        private static void IncrementStatusCountColumn(string statusCountColumnName)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("UPDATE " + Core.Db.StatusCount.DOCUMENT_NAME + " SET " + statusCountColumnName + " = " + statusCountColumnName + " + 1;", conn);
                _ = command.ExecuteNonQuery();
            }
        }

        public override void IncrementDisabledCount()
        {
            IncrementStatusCountColumn(StatusCount.COLUMN_NAME_DISABLED_COUNT);
        }

        public override void IncrementRejectedCount()
        {
            IncrementStatusCountColumn(StatusCount.COLUMN_NAME_REJECTED_COUNT);
        }

        public override void IncrementDoneCount()
        {
            IncrementStatusCountColumn(StatusCount.COLUMN_NAME_DONE_COUNT);
        }

        public override void IncrementFailedCount()
        {
            IncrementStatusCountColumn(StatusCount.COLUMN_NAME_FAILED_COUNT);
        }

        public override void IncrementPendingCount()
        {
            IncrementStatusCountColumn(StatusCount.COLUMN_NAME_PENDING_COUNT);
        }

        public override void IncrementRunningCount()
        {
            IncrementStatusCountColumn(StatusCount.COLUMN_NAME_RUNNING_COUNT);
        }

        public override void IncrementStoppedCount()
        {
            IncrementStatusCountColumn(StatusCount.COLUMN_NAME_STOPPED_COUNT);
        }

        public override void IncrementWarningCount()
        {
            IncrementStatusCountColumn(StatusCount.COLUMN_NAME_WARNING_COUNT);
        }

        private static void DecrementStatusCountColumn(string statusCountColumnName)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("UPDATE " + Core.Db.StatusCount.DOCUMENT_NAME + " SET " + statusCountColumnName + " = " + statusCountColumnName + " - 1;", conn);
                _ = command.ExecuteNonQuery();
            }
        }

        public override void DecrementPendingCount()
        {
            DecrementStatusCountColumn(StatusCount.COLUMN_NAME_PENDING_COUNT);
        }

        public override void DecrementRunningCount()
        {
            DecrementStatusCountColumn(StatusCount.COLUMN_NAME_RUNNING_COUNT);
        }

        public override void InsertEntry(Core.Db.Entry entry)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("INSERT INTO " + Core.Db.Entry.DOCUMENT_NAME + "("
                    + Entry.COLUMN_NAME_NAME + ", "
                    + Entry.COLUMN_NAME_DESCRIPTION + ", "
                    + Entry.COLUMN_NAME_LAUNCH_TYPE + ", "
                    + Entry.COLUMN_NAME_STATUS_DATE + ", "
                    + Entry.COLUMN_NAME_STATUS + ", "
                    + Entry.COLUMN_NAME_WORKFLOW_ID + ", "
                    + Entry.COLUMN_NAME_JOB_ID + ", "
                    + Entry.COLUMN_NAME_LOGS + ") VALUES("
                    + "'" + (entry.Name ?? "").Replace("'", "''").Replace("\\", "\\\\") + "'" + ", "
                    + "'" + (entry.Description ?? "").Replace("'", "''").Replace("\\", "\\\\") + "'" + ", "
                    + (int)entry.LaunchType + ", "
                    + "'" + entry.StatusDate.ToString(DATE_TIME_FORMAT) + "'" + ", "
                    + (int)entry.Status + ", "
                    + entry.WorkflowId + ", "
                    + "'" + (entry.JobId ?? "") + "', "
                    + "'" + (entry.Logs ?? "").Replace("'", "''").Replace("\\", "\\\\") + "'" + ");"
                    , conn);

                _ = command.ExecuteNonQuery();
            }
        }

        public override void InsertHistoryEntry(Core.Db.HistoryEntry entry)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("INSERT INTO " + Core.Db.HistoryEntry.DOCUMENT_NAME + "("
                    + HistoryEntry.COLUMN_NAME_NAME + ", "
                    + HistoryEntry.COLUMN_NAME_DESCRIPTION + ", "
                    + HistoryEntry.COLUMN_NAME_LAUNCH_TYPE + ", "
                    + HistoryEntry.COLUMN_NAME_STATUS_DATE + ", "
                    + HistoryEntry.COLUMN_NAME_STATUS + ", "
                    + HistoryEntry.COLUMN_NAME_WORKFLOW_ID + ", "
                    + HistoryEntry.COLUMN_NAME_LOGS + ") VALUES("
                    + "'" + (entry.Name ?? "").Replace("'", "''").Replace("\\", "\\\\") + "'" + ", "
                    + "'" + (entry.Description ?? "").Replace("'", "''").Replace("\\", "\\\\") + "'" + ", "
                    + (int)entry.LaunchType + ", "
                    + "'" + entry.StatusDate.ToString(DATE_TIME_FORMAT) + "'" + ", "
                    + (int)entry.Status + ", "
                    + entry.WorkflowId + ", "
                    + "'" + (entry.Logs ?? "").Replace("'", "''").Replace("\\", "\\\\") + "'" + ");"
                    , conn);

                _ = command.ExecuteNonQuery();
            }
        }

        public override void InsertUser(Core.Db.User user)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("INSERT INTO " + Core.Db.User.DOCUMENT_NAME + "("
                    + User.COLUMN_NAME_USERNAME + ", "
                    + User.COLUMN_NAME_PASSWORD + ", "
                    + User.COLUMN_NAME_USER_PROFILE + ", "
                    + User.COLUMN_NAME_EMAIL + ", "
                    + User.COLUMN_NAME_CREATED_ON + ", "
                    + User.COLUMN_NAME_MODIFIED_ON + ") VALUES("
                    + "'" + (user.Username ?? "").Replace("'", "''").Replace("\\", "\\\\") + "'" + ", "
                    + "'" + (user.Password ?? "").Replace("'", "''").Replace("\\", "\\\\") + "'" + ", "
                    + (int)user.UserProfile + ", "
                    + "'" + (user.Email ?? "").Replace("'", "''").Replace("\\", "\\\\") + "'" + ", "
                    + "'" + DateTime.Now.ToString(DATE_TIME_FORMAT) + "'" + ", "
                    + (user.ModifiedOn == DateTime.MinValue ? "NULL" : "'" + user.ModifiedOn.ToString(DATE_TIME_FORMAT) + "'") + ");"
                    , conn);

                _ = command.ExecuteNonQuery();
            }
        }

        public override void InsertUserWorkflowRelation(Core.Db.UserWorkflow userWorkflow)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("INSERT INTO " + Core.Db.UserWorkflow.DOCUMENT_NAME + "("
                    + UserWorkflow.COLUMN_NAME_USER_ID + ", "
                    + UserWorkflow.COLUMN_NAME_WORKFLOW_ID + ") VALUES("
                    + int.Parse(userWorkflow.UserId) + ", "
                    + int.Parse(userWorkflow.WorkflowId) + ");"
                    , conn);

                _ = command.ExecuteNonQuery();
            }
        }

        public override string InsertWorkflow(Core.Db.Workflow workflow)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("INSERT INTO " + Core.Db.Workflow.DOCUMENT_NAME + "("
                    + Workflow.COLUMN_NAME_XML + ") VALUES("
                    + "'" + (workflow.Xml ?? "").Replace("'", "''").Replace("\\", "\\\\") + "'" + "); SELECT LAST_INSERT_ID(); "
                    , conn);

                var id = (ulong)command.ExecuteScalar()!;

                return id.ToString();
            }
        }

        public override void UpdateEntry(string id, Core.Db.Entry entry)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("UPDATE " + Core.Db.Entry.DOCUMENT_NAME + " SET "
                    + Entry.COLUMN_NAME_NAME + " = '" + (entry.Name ?? "").Replace("'", "''").Replace("\\", "\\\\") + "', "
                    + Entry.COLUMN_NAME_DESCRIPTION + " = '" + (entry.Description ?? "").Replace("'", "''").Replace("\\", "\\\\") + "', "
                    + Entry.COLUMN_NAME_LAUNCH_TYPE + " = " + (int)entry.LaunchType + ", "
                    + Entry.COLUMN_NAME_STATUS_DATE + " = '" + entry.StatusDate.ToString(DATE_TIME_FORMAT) + "', "
                    + Entry.COLUMN_NAME_STATUS + " = " + (int)entry.Status + ", "
                    + Entry.COLUMN_NAME_WORKFLOW_ID + " = " + entry.WorkflowId + ", "
                    + Entry.COLUMN_NAME_JOB_ID + " = '" + (entry.JobId ?? "") + "', "
                    + Entry.COLUMN_NAME_LOGS + " = '" + (entry.Logs ?? "").Replace("'", "''").Replace("\\", "\\\\") + "'"
                    + " WHERE "
                    + Entry.COLUMN_NAME_ID + " = " + int.Parse(id) + ";"
                    , conn);

                _ = command.ExecuteNonQuery();
            }
        }

        public override void UpdatePassword(string username, string password)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("UPDATE " + Core.Db.User.DOCUMENT_NAME + " SET "
                    + User.COLUMN_NAME_PASSWORD + " = '" + (password ?? "").Replace("'", "''").Replace("\\", "\\\\") + "'"
                    + " WHERE "
                    + User.COLUMN_NAME_USERNAME + " = '" + (username ?? "").Replace("'", "''").Replace("\\", "\\\\") + "';"
                    , conn);

                _ = command.ExecuteNonQuery();
            }
        }

        public override void UpdateUser(string id, Core.Db.User user)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("UPDATE " + Core.Db.User.DOCUMENT_NAME + " SET "
                    + User.COLUMN_NAME_USERNAME + " = '" + (user.Username ?? "").Replace("'", "''").Replace("\\", "\\\\") + "', "
                    + User.COLUMN_NAME_PASSWORD + " = '" + (user.Password ?? "").Replace("'", "''").Replace("\\", "\\\\") + "', "
                    + User.COLUMN_NAME_USER_PROFILE + " = " + (int)user.UserProfile + ", "
                    + User.COLUMN_NAME_EMAIL + " = '" + (user.Email ?? "").Replace("'", "''").Replace("\\", "\\\\") + "', "
                    + User.COLUMN_NAME_CREATED_ON + " = '" + user.CreatedOn.ToString(DATE_TIME_FORMAT) + "', "
                    + User.COLUMN_NAME_MODIFIED_ON + " = '" + DateTime.Now.ToString(DATE_TIME_FORMAT) + "'"
                    + " WHERE "
                    + User.COLUMN_NAME_ID + " = " + int.Parse(id) + ";"
                    , conn);

                _ = command.ExecuteNonQuery();
            }
        }

        public override void UpdateUsernameAndEmailAndUserProfile(string userId, string username, string email, UserProfile up)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("UPDATE " + Core.Db.User.DOCUMENT_NAME + " SET "
                    + User.COLUMN_NAME_USERNAME + " = '" + (username ?? "").Replace("'", "''").Replace("\\", "\\\\") + "', "
                    + User.COLUMN_NAME_USER_PROFILE + " = " + (int)up + ", "
                    + User.COLUMN_NAME_EMAIL + " = '" + (email ?? "").Replace("'", "''").Replace("\\", "\\\\") + "', "
                    + User.COLUMN_NAME_MODIFIED_ON + " = '" + DateTime.Now.ToString(DATE_TIME_FORMAT) + "'"
                    + " WHERE "
                    + User.COLUMN_NAME_ID + " = " + int.Parse(userId) + ";"
                    , conn);

                _ = command.ExecuteNonQuery();
            }
        }

        public override void UpdateWorkflow(string dbId, Core.Db.Workflow workflow)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("UPDATE " + Core.Db.Workflow.DOCUMENT_NAME + " SET "
                    + Workflow.COLUMN_NAME_XML + " = '" + (workflow.Xml ?? "").Replace("'", "''").Replace("\\", "\\\\") + "'"
                    + " WHERE "
                    + User.COLUMN_NAME_ID + " = " + int.Parse(dbId) + ";"
                    , conn);

                _ = command.ExecuteNonQuery();
            }
        }

        public override string GetEntryLogs(string entryId)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT " + Entry.COLUMN_NAME_LOGS
                    + " FROM " + Core.Db.Entry.DOCUMENT_NAME
                    + " WHERE "
                    + Entry.COLUMN_NAME_ID + " = " + int.Parse(entryId) + ";"
                    , conn);

                using var reader = command.ExecuteReader();

                if (reader.Read())
                {
                    var logs = (string)reader[Entry.COLUMN_NAME_LOGS];
                    return logs;
                }

                return null;
            }
        }

        public override string GetHistoryEntryLogs(string entryId)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT " + HistoryEntry.COLUMN_NAME_LOGS
                    + " FROM " + Core.Db.HistoryEntry.DOCUMENT_NAME
                    + " WHERE "
                    + HistoryEntry.COLUMN_NAME_ID + " = " + int.Parse(entryId) + ";"
                    , conn);

                using var reader = command.ExecuteReader();

                if (reader.Read())
                {
                    var logs = (string)reader[HistoryEntry.COLUMN_NAME_LOGS];
                    return logs;
                }

                return null;
            }
        }

        public override IEnumerable<Core.Db.User> GetNonRestricedUsers()
        {
            lock (Padlock)
            {
                List<User> users = new();

                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT "
                                                 + User.COLUMN_NAME_ID + ", "
                                                 + User.COLUMN_NAME_USERNAME + ", "
                                                 + User.COLUMN_NAME_PASSWORD + ", "
                                                 + User.COLUMN_NAME_EMAIL + ", "
                                                 + User.COLUMN_NAME_USER_PROFILE + ", "
                                                 + User.COLUMN_NAME_CREATED_ON + ", "
                                                 + User.COLUMN_NAME_MODIFIED_ON
                                                 + " FROM " + Core.Db.User.DOCUMENT_NAME
                                                 + " WHERE (" + User.COLUMN_NAME_USER_PROFILE + " = " + (int)UserProfile.SuperAdministrator
                                                 + " OR " + User.COLUMN_NAME_USER_PROFILE + " = " + (int)UserProfile.Administrator + ")"
                                                 + " ORDER BY " + User.COLUMN_NAME_USERNAME
                                                 + ";", conn);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    User admin = new()
                    {
                        Id = (int)reader[User.COLUMN_NAME_ID],
                        Username = (string)reader[User.COLUMN_NAME_USERNAME],
                        Password = (string)reader[User.COLUMN_NAME_PASSWORD],
                        Email = (string)reader[User.COLUMN_NAME_EMAIL],
                        UserProfile = (UserProfile)(int)reader[User.COLUMN_NAME_USER_PROFILE],
                        CreatedOn = (DateTime)reader[User.COLUMN_NAME_CREATED_ON],
                        ModifiedOn = reader[User.COLUMN_NAME_MODIFIED_ON] == DBNull.Value ? DateTime.MinValue : (DateTime)reader[User.COLUMN_NAME_MODIFIED_ON]
                    };

                    users.Add(admin);
                }

                return users;
            }
        }

        public override string InsertRecord(Core.Db.Record record)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("INSERT INTO " + Core.Db.Record.DOCUMENT_NAME + "("
                    + Record.COLUMN_NAME_NAME + ", "
                    + Record.COLUMN_NAME_DESCRIPTION + ", "
                    + Record.COLUMN_NAME_APPROVED + ", "
                    + Record.COLUMN_NAME_START_DATE + ", "
                    + Record.COLUMN_NAME_END_DATE + ", "
                    + Record.COLUMN_NAME_COMMENTS + ", "
                    + Record.COLUMN_NAME_MANAGER_COMMENTS + ", "
                    + Record.COLUMN_NAME_CREATED_BY + ", "
                    + Record.COLUMN_NAME_CREATED_ON + ", "
                    + Record.COLUMN_NAME_MODIFIED_BY + ", "
                    + Record.COLUMN_NAME_MODIFIED_ON + ", "
                    + Record.COLUMN_NAME_ASSIGNED_TO + ", "
                    + Record.COLUMN_NAME_ASSIGNED_ON + ")"
                    + " VALUES("
                    + "'" + (record.Name ?? "").Replace("'", "''").Replace("\\", "\\\\") + "'" + ", "
                    + "'" + (record.Description ?? "").Replace("'", "''").Replace("\\", "\\\\") + "'" + ", "
                    + (record.Approved ? "1" : "0") + ", "
                    + (record.StartDate == null ? "NULL" : "'" + record.StartDate.Value.ToString(DATE_TIME_FORMAT) + "'") + ", "
                    + (record.EndDate == null ? "NULL" : "'" + record.EndDate.Value.ToString(DATE_TIME_FORMAT) + "'") + ", "
                    + "'" + (record.Comments ?? "").Replace("'", "''").Replace("\\", "\\\\") + "'" + ", "
                    + "'" + (record.ManagerComments ?? "").Replace("'", "''").Replace("\\", "\\\\") + "'" + ", "
                    + int.Parse(record.CreatedBy) + ", "
                    + "'" + DateTime.Now.ToString(DATE_TIME_FORMAT) + "'" + ", "
                    + (string.IsNullOrEmpty(record.ModifiedBy) ? "NULL" : int.Parse(record.ModifiedBy).ToString()) + ", "
                    + (record.ModifiedOn == null ? "NULL" : "'" + record.ModifiedOn.Value.ToString(DATE_TIME_FORMAT) + "'") + ", "
                     + (string.IsNullOrEmpty(record.AssignedTo) ? "NULL" : int.Parse(record.AssignedTo).ToString()) + ", "
                    + (record.AssignedOn == null ? "NULL" : "'" + record.AssignedOn.Value.ToString(DATE_TIME_FORMAT) + "'") + ");"
                    + " SELECT LAST_INSERT_ID();"
                    , conn);
                var id = (ulong)command.ExecuteScalar()!;
                return id.ToString();
            }
        }

        public override void UpdateRecord(string recordId, Core.Db.Record record)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("UPDATE " + Core.Db.Record.DOCUMENT_NAME + " SET "
                    + Record.COLUMN_NAME_NAME + " = '" + (record.Name ?? "").Replace("'", "''").Replace("\\", "\\\\") + "', "
                    + Record.COLUMN_NAME_DESCRIPTION + " = '" + (record.Description ?? "").Replace("'", "''").Replace("\\", "\\\\") + "', "
                    + Record.COLUMN_NAME_APPROVED + " = " + (record.Approved ? "1" : "0") + ", "
                    + Record.COLUMN_NAME_START_DATE + " = " + (record.StartDate == null ? "NULL" : "'" + record.StartDate.Value.ToString(DATE_TIME_FORMAT) + "'") + ", "
                    + Record.COLUMN_NAME_END_DATE + " = " + (record.EndDate == null ? "NULL" : "'" + record.EndDate.Value.ToString(DATE_TIME_FORMAT) + "'") + ", "
                    + Record.COLUMN_NAME_COMMENTS + " = '" + (record.Comments ?? "").Replace("'", "''").Replace("\\", "\\\\") + "', "
                    + Record.COLUMN_NAME_MANAGER_COMMENTS + " = '" + (record.ManagerComments ?? "").Replace("'", "''").Replace("\\", "\\\\") + "', "
                    + Record.COLUMN_NAME_CREATED_BY + " = " + int.Parse(record.CreatedBy) + ", "
                    + Record.COLUMN_NAME_MODIFIED_BY + " = " + (string.IsNullOrEmpty(record.ModifiedBy) ? "NULL" : int.Parse(record.ModifiedBy).ToString()) + ", "
                    + Record.COLUMN_NAME_MODIFIED_ON + " = '" + DateTime.Now.ToString(DATE_TIME_FORMAT) + "', "
                    + Record.COLUMN_NAME_ASSIGNED_TO + " = " + (string.IsNullOrEmpty(record.AssignedTo) ? "NULL" : int.Parse(record.AssignedTo).ToString()) + ", "
                    + Record.COLUMN_NAME_ASSIGNED_ON + " = " + (record.AssignedOn == null ? "NULL" : "'" + record.AssignedOn.Value.ToString(DATE_TIME_FORMAT) + "'")
                    + " WHERE "
                    + Record.COLUMN_NAME_ID + " = " + int.Parse(recordId) + ";"
                    , conn);
                _ = command.ExecuteNonQuery();
            }
        }

        public override void DeleteRecords(string[] recordIds)
        {
            lock (Padlock)
            {
                if (recordIds.Length > 0)
                {
                    using MySqlConnection conn = new(_connectionString);
                    conn.Open();

                    StringBuilder builder = new("(");

                    for (var i = 0; i < recordIds.Length; i++)
                    {
                        var id = recordIds[i];
                        _ = builder.Append(id);
                        _ = i < recordIds.Length - 1 ? builder.Append(", ") : builder.Append(')');
                    }

                    using MySqlCommand command = new("DELETE FROM " + Core.Db.Record.DOCUMENT_NAME
                        + " WHERE " + Record.COLUMN_NAME_ID + " IN " + builder + ";", conn);
                    _ = command.ExecuteNonQuery();
                }
            }
        }

        public override Core.Db.Record GetRecord(string id)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT "
                    + Record.COLUMN_NAME_ID + ", "
                    + Record.COLUMN_NAME_NAME + ", "
                    + Record.COLUMN_NAME_DESCRIPTION + ", "
                    + Record.COLUMN_NAME_APPROVED + ", "
                    + Record.COLUMN_NAME_START_DATE + ", "
                    + Record.COLUMN_NAME_END_DATE + ", "
                    + Record.COLUMN_NAME_COMMENTS + ", "
                    + Record.COLUMN_NAME_MANAGER_COMMENTS + ", "
                    + Record.COLUMN_NAME_CREATED_BY + ", "
                    + Record.COLUMN_NAME_CREATED_ON + ", "
                    + Record.COLUMN_NAME_MODIFIED_BY + ", "
                    + Record.COLUMN_NAME_MODIFIED_ON + ", "
                    + Record.COLUMN_NAME_ASSIGNED_TO + ", "
                    + Record.COLUMN_NAME_ASSIGNED_ON
                    + " FROM " + Core.Db.Record.DOCUMENT_NAME
                    + " WHERE " + Record.COLUMN_NAME_ID + " = " + int.Parse(id)
                    + ";", conn);
                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    Record record = new()
                    {
                        Id = (int)reader[Record.COLUMN_NAME_ID],
                        Name = (string)reader[Record.COLUMN_NAME_NAME],
                        Description = (string)reader[Record.COLUMN_NAME_DESCRIPTION],
                        Approved = (ulong)reader[Record.COLUMN_NAME_APPROVED] == 1,
                        StartDate = reader[Record.COLUMN_NAME_START_DATE] == DBNull.Value ? null : (DateTime?)reader[Record.COLUMN_NAME_START_DATE],
                        EndDate = reader[Record.COLUMN_NAME_END_DATE] == DBNull.Value ? null : (DateTime?)reader[Record.COLUMN_NAME_END_DATE],
                        Comments = (string)reader[Record.COLUMN_NAME_COMMENTS],
                        ManagerComments = (string)reader[Record.COLUMN_NAME_MANAGER_COMMENTS],
                        CreatedBy = ((int)reader[Record.COLUMN_NAME_CREATED_BY]).ToString(),
                        CreatedOn = (DateTime)reader[Record.COLUMN_NAME_CREATED_ON],
                        ModifiedBy = reader[Record.COLUMN_NAME_MODIFIED_BY] == DBNull.Value ? string.Empty : ((int)reader[Record.COLUMN_NAME_MODIFIED_BY]).ToString(),
                        ModifiedOn = reader[Record.COLUMN_NAME_MODIFIED_ON] == DBNull.Value ? null : (DateTime?)reader[Record.COLUMN_NAME_MODIFIED_ON],
                        AssignedTo = reader[Record.COLUMN_NAME_ASSIGNED_TO] == DBNull.Value ? string.Empty : ((int)reader[Record.COLUMN_NAME_ASSIGNED_TO]).ToString(),
                        AssignedOn = reader[Record.COLUMN_NAME_ASSIGNED_ON] == DBNull.Value ? null : (DateTime?)reader[Record.COLUMN_NAME_ASSIGNED_ON]
                    };

                    return record;
                }

                return null;
            }
        }

        public override IEnumerable<Core.Db.Record> GetRecords(string keyword)
        {
            lock (Padlock)
            {
                List<Record> records = new();

                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT "
                                                 + Record.COLUMN_NAME_ID + ", "
                                                 + Record.COLUMN_NAME_NAME + ", "
                                                 + Record.COLUMN_NAME_DESCRIPTION + ", "
                                                 + Record.COLUMN_NAME_APPROVED + ", "
                                                 + Record.COLUMN_NAME_START_DATE + ", "
                                                 + Record.COLUMN_NAME_END_DATE + ", "
                                                 + Record.COLUMN_NAME_COMMENTS + ", "
                                                 + Record.COLUMN_NAME_MANAGER_COMMENTS + ", "
                                                 + Record.COLUMN_NAME_CREATED_BY + ", "
                                                 + Record.COLUMN_NAME_CREATED_ON + ", "
                                                 + Record.COLUMN_NAME_MODIFIED_BY + ", "
                                                 + Record.COLUMN_NAME_MODIFIED_ON + ", "
                                                 + Record.COLUMN_NAME_ASSIGNED_TO + ", "
                                                 + Record.COLUMN_NAME_ASSIGNED_ON
                                                 + " FROM " + Core.Db.Record.DOCUMENT_NAME
                                                 + " WHERE " + "LOWER(" + Record.COLUMN_NAME_NAME + ")" + " LIKE '%" + (keyword ?? "").Replace("'", "''").ToLower() + "%'"
                                                 + " OR " + "LOWER(" + Record.COLUMN_NAME_DESCRIPTION + ")" + " LIKE '%" + (keyword ?? "").Replace("'", "''").ToLower() + "%'"
                                                 + " ORDER BY " + Record.COLUMN_NAME_CREATED_ON + " DESC"
                                                 + ";", conn);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    Record record = new()
                    {
                        Id = (int)reader[Record.COLUMN_NAME_ID],
                        Name = (string)reader[Record.COLUMN_NAME_NAME],
                        Description = (string)reader[Record.COLUMN_NAME_DESCRIPTION],
                        Approved = (ulong)reader[Record.COLUMN_NAME_APPROVED] == 1,
                        StartDate = reader[Record.COLUMN_NAME_START_DATE] == DBNull.Value ? null : (DateTime?)reader[Record.COLUMN_NAME_START_DATE],
                        EndDate = reader[Record.COLUMN_NAME_END_DATE] == DBNull.Value ? null : (DateTime?)reader[Record.COLUMN_NAME_END_DATE],
                        Comments = (string)reader[Record.COLUMN_NAME_COMMENTS],
                        ManagerComments = (string)reader[Record.COLUMN_NAME_MANAGER_COMMENTS],
                        CreatedBy = ((int)reader[Record.COLUMN_NAME_CREATED_BY]).ToString(),
                        CreatedOn = (DateTime)reader[Record.COLUMN_NAME_CREATED_ON],
                        ModifiedBy = reader[Record.COLUMN_NAME_MODIFIED_BY] == DBNull.Value ? string.Empty : ((int)reader[Record.COLUMN_NAME_MODIFIED_BY]).ToString(),
                        ModifiedOn = reader[Record.COLUMN_NAME_MODIFIED_ON] == DBNull.Value ? null : (DateTime?)reader[Record.COLUMN_NAME_MODIFIED_ON],
                        AssignedTo = reader[Record.COLUMN_NAME_ASSIGNED_TO] == DBNull.Value ? string.Empty : ((int)reader[Record.COLUMN_NAME_ASSIGNED_TO]).ToString(),
                        AssignedOn = reader[Record.COLUMN_NAME_ASSIGNED_ON] == DBNull.Value ? null : (DateTime?)reader[Record.COLUMN_NAME_ASSIGNED_ON]
                    };

                    records.Add(record);
                }

                return records;
            }
        }

        public override IEnumerable<Core.Db.Record> GetRecordsCreatedBy(string createdBy)
        {
            lock (Padlock)
            {
                List<Record> records = new();

                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT "
                                                 + Record.COLUMN_NAME_ID + ", "
                                                 + Record.COLUMN_NAME_NAME + ", "
                                                 + Record.COLUMN_NAME_DESCRIPTION + ", "
                                                 + Record.COLUMN_NAME_APPROVED + ", "
                                                 + Record.COLUMN_NAME_START_DATE + ", "
                                                 + Record.COLUMN_NAME_END_DATE + ", "
                                                 + Record.COLUMN_NAME_COMMENTS + ", "
                                                 + Record.COLUMN_NAME_MANAGER_COMMENTS + ", "
                                                 + Record.COLUMN_NAME_CREATED_BY + ", "
                                                 + Record.COLUMN_NAME_CREATED_ON + ", "
                                                 + Record.COLUMN_NAME_MODIFIED_BY + ", "
                                                 + Record.COLUMN_NAME_MODIFIED_ON + ", "
                                                 + Record.COLUMN_NAME_ASSIGNED_TO + ", "
                                                 + Record.COLUMN_NAME_ASSIGNED_ON
                                                 + " FROM " + Core.Db.Record.DOCUMENT_NAME
                                                 + " WHERE " + Record.COLUMN_NAME_CREATED_BY + " = " + int.Parse(createdBy)
                                                 + " ORDER BY " + Record.COLUMN_NAME_NAME + " ASC"
                                                 + ";", conn);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    Record record = new()
                    {
                        Id = (int)reader[Record.COLUMN_NAME_ID],
                        Name = (string)reader[Record.COLUMN_NAME_NAME],
                        Description = (string)reader[Record.COLUMN_NAME_DESCRIPTION],
                        Approved = (ulong)reader[Record.COLUMN_NAME_APPROVED] == 1,
                        StartDate = reader[Record.COLUMN_NAME_START_DATE] == DBNull.Value ? null : (DateTime?)reader[Record.COLUMN_NAME_START_DATE],
                        EndDate = reader[Record.COLUMN_NAME_END_DATE] == DBNull.Value ? null : (DateTime?)reader[Record.COLUMN_NAME_END_DATE],
                        Comments = (string)reader[Record.COLUMN_NAME_COMMENTS],
                        ManagerComments = (string)reader[Record.COLUMN_NAME_MANAGER_COMMENTS],
                        CreatedBy = ((int)reader[Record.COLUMN_NAME_CREATED_BY]).ToString(),
                        CreatedOn = (DateTime)reader[Record.COLUMN_NAME_CREATED_ON],
                        ModifiedBy = reader[Record.COLUMN_NAME_MODIFIED_BY] == DBNull.Value ? string.Empty : ((int)reader[Record.COLUMN_NAME_MODIFIED_BY]).ToString(),
                        ModifiedOn = reader[Record.COLUMN_NAME_MODIFIED_ON] == DBNull.Value ? null : (DateTime?)reader[Record.COLUMN_NAME_MODIFIED_ON],
                        AssignedTo = reader[Record.COLUMN_NAME_ASSIGNED_TO] == DBNull.Value ? string.Empty : ((int)reader[Record.COLUMN_NAME_ASSIGNED_TO]).ToString(),
                        AssignedOn = reader[Record.COLUMN_NAME_ASSIGNED_ON] == DBNull.Value ? null : (DateTime?)reader[Record.COLUMN_NAME_ASSIGNED_ON]
                    };

                    records.Add(record);
                }

                return records;
            }
        }

        public override IEnumerable<Core.Db.Record> GetRecordsCreatedByOrAssignedTo(string createdBy, string assingedTo, string keyword)
        {
            lock (Padlock)
            {
                List<Record> records = new();

                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT "
                                                 + Record.COLUMN_NAME_ID + ", "
                                                 + Record.COLUMN_NAME_NAME + ", "
                                                 + Record.COLUMN_NAME_DESCRIPTION + ", "
                                                 + Record.COLUMN_NAME_APPROVED + ", "
                                                 + Record.COLUMN_NAME_START_DATE + ", "
                                                 + Record.COLUMN_NAME_END_DATE + ", "
                                                 + Record.COLUMN_NAME_COMMENTS + ", "
                                                 + Record.COLUMN_NAME_MANAGER_COMMENTS + ", "
                                                 + Record.COLUMN_NAME_CREATED_BY + ", "
                                                 + Record.COLUMN_NAME_CREATED_ON + ", "
                                                 + Record.COLUMN_NAME_MODIFIED_BY + ", "
                                                 + Record.COLUMN_NAME_MODIFIED_ON + ", "
                                                 + Record.COLUMN_NAME_ASSIGNED_TO + ", "
                                                 + Record.COLUMN_NAME_ASSIGNED_ON
                                                 + " FROM " + Core.Db.Record.DOCUMENT_NAME
                                                 + " WHERE " + "(LOWER(" + Record.COLUMN_NAME_NAME + ")" + " LIKE '%" + (keyword ?? "").Replace("'", "''").ToLower() + "%'"
                                                 + " OR " + "LOWER(" + Record.COLUMN_NAME_DESCRIPTION + ")" + " LIKE '%" + (keyword ?? "").Replace("'", "''").ToLower() + "%')"
                                                 + " AND (" + Record.COLUMN_NAME_CREATED_BY + " = " + int.Parse(createdBy) + " OR " + Record.COLUMN_NAME_ASSIGNED_TO + " = " + int.Parse(assingedTo) + ")"
                                                 + " ORDER BY " + Record.COLUMN_NAME_CREATED_ON + " DESC"
                                                 + ";", conn);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    Record record = new()
                    {
                        Id = (int)reader[Record.COLUMN_NAME_ID],
                        Name = (string)reader[Record.COLUMN_NAME_NAME],
                        Description = (string)reader[Record.COLUMN_NAME_DESCRIPTION],
                        Approved = (ulong)reader[Record.COLUMN_NAME_APPROVED] == 1,
                        StartDate = reader[Record.COLUMN_NAME_START_DATE] == DBNull.Value ? null : (DateTime?)reader[Record.COLUMN_NAME_START_DATE],
                        EndDate = reader[Record.COLUMN_NAME_END_DATE] == DBNull.Value ? null : (DateTime?)reader[Record.COLUMN_NAME_END_DATE],
                        Comments = (string)reader[Record.COLUMN_NAME_COMMENTS],
                        ManagerComments = (string)reader[Record.COLUMN_NAME_MANAGER_COMMENTS],
                        CreatedBy = ((int)reader[Record.COLUMN_NAME_CREATED_BY]).ToString(),
                        CreatedOn = (DateTime)reader[Record.COLUMN_NAME_CREATED_ON],
                        ModifiedBy = reader[Record.COLUMN_NAME_MODIFIED_BY] == DBNull.Value ? string.Empty : ((int)reader[Record.COLUMN_NAME_MODIFIED_BY]).ToString(),
                        ModifiedOn = reader[Record.COLUMN_NAME_MODIFIED_ON] == DBNull.Value ? null : (DateTime?)reader[Record.COLUMN_NAME_MODIFIED_ON],
                        AssignedTo = reader[Record.COLUMN_NAME_ASSIGNED_TO] == DBNull.Value ? string.Empty : ((int)reader[Record.COLUMN_NAME_ASSIGNED_TO]).ToString(),
                        AssignedOn = reader[Record.COLUMN_NAME_ASSIGNED_ON] == DBNull.Value ? null : (DateTime?)reader[Record.COLUMN_NAME_ASSIGNED_ON]
                    };

                    records.Add(record);
                }

                return records;
            }
        }

        public override string InsertVersion(Core.Db.Version version)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("INSERT INTO " + Core.Db.Version.DOCUMENT_NAME + "("
                    + Version.COLUMN_NAME_RECORD_ID + ", "
                    + Version.COLUMN_NAME_FILE_PATH + ", "
                    + Version.COLUMN_NAME_CREATED_ON + ")"
                    + " VALUES("
                    + int.Parse(version.RecordId) + ", "
                    + "'" + (version.FilePath ?? "").Replace("'", "''").Replace("\\", "\\\\") + "'" + ", "
                    + "'" + DateTime.Now.ToString(DATE_TIME_FORMAT) + "'" + ");"
                    + " SELECT LAST_INSERT_ID();"
                    , conn);
                var id = (ulong)command.ExecuteScalar()!;
                return id.ToString();
            }
        }

        public override void UpdateVersion(string versionId, Core.Db.Version version)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("UPDATE " + Core.Db.Version.DOCUMENT_NAME + " SET "
                    + Version.COLUMN_NAME_RECORD_ID + " = " + int.Parse(version.RecordId) + ", "
                    + Version.COLUMN_NAME_FILE_PATH + " = '" + (version.FilePath ?? "").Replace("'", "''").Replace("\\", "\\\\") + "'"
                    + " WHERE "
                    + Version.COLUMN_NAME_ID + " = " + int.Parse(versionId) + ";"
                    , conn);
                _ = command.ExecuteNonQuery();
            }
        }

        public override void DeleteVersions(string[] versionIds)
        {
            lock (Padlock)
            {
                if (versionIds.Length > 0)
                {
                    using MySqlConnection conn = new(_connectionString);
                    conn.Open();

                    StringBuilder builder = new("(");

                    for (var i = 0; i < versionIds.Length; i++)
                    {
                        var id = versionIds[i];
                        _ = builder.Append(id);
                        _ = i < versionIds.Length - 1 ? builder.Append(", ") : builder.Append(')');
                    }

                    using MySqlCommand command = new("DELETE FROM " + Core.Db.Version.DOCUMENT_NAME
                        + " WHERE " + Version.COLUMN_NAME_ID + " IN " + builder + ";", conn);
                    _ = command.ExecuteNonQuery();
                }
            }
        }

        public override IEnumerable<Core.Db.Version> GetVersions(string recordId)
        {
            lock (Padlock)
            {
                List<Version> versions = new();

                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT "
                                                 + Version.COLUMN_NAME_ID + ", "
                                                 + Version.COLUMN_NAME_RECORD_ID + ", "
                                                 + Version.COLUMN_NAME_FILE_PATH + ", "
                                                 + Version.COLUMN_NAME_CREATED_ON
                                                 + " FROM " + Core.Db.Version.DOCUMENT_NAME
                                                 + " WHERE " + Version.COLUMN_NAME_RECORD_ID + " = " + int.Parse(recordId)
                                                 + ";", conn);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    Version version = new()
                    {
                        Id = (int)reader[Version.COLUMN_NAME_ID],
                        RecordId = ((int)reader[Version.COLUMN_NAME_RECORD_ID]).ToString(),
                        FilePath = (string)reader[Version.COLUMN_NAME_FILE_PATH],
                        CreatedOn = (DateTime)reader[Version.COLUMN_NAME_CREATED_ON]
                    };

                    versions.Add(version);
                }

                return versions;
            }
        }

        public override Core.Db.Version GetLatestVersion(string recordId)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT "
                    + Version.COLUMN_NAME_ID + ", "
                    + Version.COLUMN_NAME_RECORD_ID + ", "
                    + Version.COLUMN_NAME_FILE_PATH + ", "
                    + Version.COLUMN_NAME_CREATED_ON
                    + " FROM " + Core.Db.Version.DOCUMENT_NAME
                    + " WHERE " + Version.COLUMN_NAME_RECORD_ID + " = " + int.Parse(recordId)
                    + " ORDER BY " + Version.COLUMN_NAME_CREATED_ON + " DESC"
                    + " LIMIT 1"
                    + ";", conn);
                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    Version version = new()
                    {
                        Id = (int)reader[Version.COLUMN_NAME_ID],
                        RecordId = ((int)reader[Version.COLUMN_NAME_RECORD_ID]).ToString(),
                        FilePath = (string)reader[Version.COLUMN_NAME_FILE_PATH],
                        CreatedOn = (DateTime)reader[Version.COLUMN_NAME_CREATED_ON]
                    };

                    return version;
                }

                return null;
            }
        }

        public override string InsertNotification(Core.Db.Notification notification)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("INSERT INTO " + Core.Db.Notification.DOCUMENT_NAME + "("
                    + Notification.COLUMN_NAME_ASSIGNED_BY + ", "
                    + Notification.COLUMN_NAME_ASSIGNED_ON + ", "
                    + Notification.COLUMN_NAME_ASSIGNED_TO + ", "
                    + Notification.COLUMN_NAME_MESSAGE + ", "
                    + Notification.COLUMN_NAME_IS_READ + ")"
                    + " VALUES("
                    + (!string.IsNullOrEmpty(notification.AssignedBy) ? int.Parse(notification.AssignedBy).ToString() : "NULL") + ", "
                    + "'" + notification.AssignedOn.ToString(DATE_TIME_FORMAT) + "'" + ", "
                    + (!string.IsNullOrEmpty(notification.AssignedTo) ? int.Parse(notification.AssignedTo).ToString() : "NULL") + ", "
                    + "'" + (notification.Message ?? "").Replace("'", "''").Replace("\\", "\\\\") + "'" + ", "
                    + (notification.IsRead ? "1" : "0") + ");"
                    + " SELECT LAST_INSERT_ID();"
                    , conn);
                var id = (ulong)command.ExecuteScalar()!;
                return id.ToString();
            }
        }

        public override void MarkNotificationsAsRead(string[] notificationIds)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                StringBuilder builder = new("(");

                for (var i = 0; i < notificationIds.Length; i++)
                {
                    var id = notificationIds[i];
                    _ = builder.Append(id);
                    _ = i < notificationIds.Length - 1 ? builder.Append(", ") : builder.Append(')');
                }

                using MySqlCommand command = new("UPDATE " + Core.Db.Notification.DOCUMENT_NAME
                    + " SET " + Notification.COLUMN_NAME_IS_READ + " = " + "1"
                    + " WHERE " + Notification.COLUMN_NAME_ID + " IN " + builder + ";", conn);
                _ = command.ExecuteNonQuery();
            }
        }

        public override void MarkNotificationsAsUnread(string[] notificationIds)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                StringBuilder builder = new("(");

                for (var i = 0; i < notificationIds.Length; i++)
                {
                    var id = notificationIds[i];
                    _ = builder.Append(id);
                    _ = i < notificationIds.Length - 1 ? builder.Append(", ") : builder.Append(')');
                }

                using MySqlCommand command = new("UPDATE " + Core.Db.Notification.DOCUMENT_NAME
                    + " SET " + Notification.COLUMN_NAME_IS_READ + " = " + "0"
                    + " WHERE " + Notification.COLUMN_NAME_ID + " IN " + builder + ";", conn);
                _ = command.ExecuteNonQuery();
            }
        }

        public override void DeleteNotifications(string[] notificationIds)
        {
            lock (Padlock)
            {
                if (notificationIds.Length > 0)
                {
                    using MySqlConnection conn = new(_connectionString);
                    conn.Open();

                    StringBuilder builder = new("(");

                    for (var i = 0; i < notificationIds.Length; i++)
                    {
                        var id = notificationIds[i];
                        _ = builder.Append(id);
                        _ = i < notificationIds.Length - 1 ? builder.Append(", ") : builder.Append(')');
                    }

                    using MySqlCommand command = new("DELETE FROM " + Core.Db.Notification.DOCUMENT_NAME
                        + " WHERE " + Notification.COLUMN_NAME_ID + " IN " + builder + ";", conn);
                    _ = command.ExecuteNonQuery();
                }
            }
        }

        public override IEnumerable<Core.Db.Notification> GetNotifications(string assignedTo, string keyword)
        {
            lock (Padlock)
            {
                List<Notification> notifications = new();

                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT "
                                                 + Notification.COLUMN_NAME_ID + ", "
                                                 + Notification.COLUMN_NAME_ASSIGNED_BY + ", "
                                                 + Notification.COLUMN_NAME_ASSIGNED_ON + ", "
                                                 + Notification.COLUMN_NAME_ASSIGNED_TO + ", "
                                                 + Notification.COLUMN_NAME_MESSAGE + ", "
                                                 + Notification.COLUMN_NAME_IS_READ
                                                 + " FROM " + Core.Db.Notification.DOCUMENT_NAME
                                                 + " WHERE " + "(LOWER(" + Notification.COLUMN_NAME_MESSAGE + ")" + " LIKE '%" + (keyword ?? "").Replace("'", "''").ToLower() + "%'"
                                                 + " AND " + Notification.COLUMN_NAME_ASSIGNED_TO + " = " + int.Parse(assignedTo) + ")"
                                                 + " ORDER BY " + Notification.COLUMN_NAME_ASSIGNED_ON + " DESC"
                                                 + ";", conn);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    Notification notification = new()
                    {
                        Id = (int)reader[Notification.COLUMN_NAME_ID],
                        AssignedBy = ((int)reader[Notification.COLUMN_NAME_ASSIGNED_BY]).ToString(),
                        AssignedOn = (DateTime)reader[Notification.COLUMN_NAME_ASSIGNED_ON],
                        AssignedTo = ((int)reader[Notification.COLUMN_NAME_ASSIGNED_TO]).ToString(),
                        Message = (string)reader[Notification.COLUMN_NAME_MESSAGE],
                        IsRead = (ulong)reader[Notification.COLUMN_NAME_IS_READ] == 1
                    };

                    notifications.Add(notification);
                }

                return notifications;
            }
        }

        public override bool HasNotifications(string assignedTo)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT COUNT(*)"
                    + " FROM " + Core.Db.Notification.DOCUMENT_NAME
                    + " WHERE (" + Notification.COLUMN_NAME_ASSIGNED_TO + " = " + int.Parse(assignedTo)
                    + " AND " + Notification.COLUMN_NAME_IS_READ + " = " + "0" + ")"
                    + ";", conn);
                var count = (long)command.ExecuteScalar()!;
                var hasNotifications = count > 0;
                return hasNotifications;
            }
        }

        public override string InsertApprover(Core.Db.Approver approver)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("INSERT INTO " + Core.Db.Approver.DOCUMENT_NAME + "("
                    + Approver.COLUMN_NAME_USER_ID + ", "
                    + Approver.COLUMN_NAME_RECORD_ID + ", "
                    + Approver.COLUMN_NAME_APPROVED + ", "
                    + Approver.COLUMN_NAME_APPROVED_ON + ") VALUES("
                    + int.Parse(approver.UserId) + ", "
                    + int.Parse(approver.RecordId) + ", "
                    + (approver.Approved ? "1" : "0") + ", "
                    + (approver.ApprovedOn == null ? "NULL" : "'" + approver.ApprovedOn.Value.ToString(DATE_TIME_FORMAT) + "'") + ");"
                    + " SELECT LAST_INSERT_ID();"
                    , conn);
                var id = (ulong)command.ExecuteScalar()!;
                return id.ToString();
            }
        }

        public override void UpdateApprover(string approverId, Core.Db.Approver approver)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("UPDATE " + Core.Db.Approver.DOCUMENT_NAME + " SET "
                    + Approver.COLUMN_NAME_USER_ID + " = " + int.Parse(approver.UserId) + ", "
                    + Approver.COLUMN_NAME_RECORD_ID + " = " + int.Parse(approver.RecordId) + ", "
                    + Approver.COLUMN_NAME_APPROVED + " = " + (approver.Approved ? "1" : "0") + ", "
                    + Approver.COLUMN_NAME_APPROVED_ON + " = " + (approver.ApprovedOn == null ? "NULL" : "'" + approver.ApprovedOn.Value.ToString(DATE_TIME_FORMAT) + "'")
                    + " WHERE "
                    + Approver.COLUMN_NAME_ID + " = " + int.Parse(approverId) + ";"
                    , conn);
                _ = command.ExecuteNonQuery();
            }
        }

        public override void DeleteApproversByRecordId(string recordId)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("DELETE FROM " + Core.Db.Approver.DOCUMENT_NAME
                    + " WHERE " + Approver.COLUMN_NAME_RECORD_ID + " = " + int.Parse(recordId) + ";", conn);
                _ = command.ExecuteNonQuery();
            }
        }

        public override void DeleteApprovedApprovers(string recordId)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("DELETE FROM " + Core.Db.Approver.DOCUMENT_NAME
                    + " WHERE " + Approver.COLUMN_NAME_RECORD_ID + " = " + int.Parse(recordId)
                    + " AND " + Approver.COLUMN_NAME_APPROVED + " = " + "1"
                    + ";"
                    , conn);
                _ = command.ExecuteNonQuery();
            }
        }

        public override void DeleteApproversByUserId(string userId)
        {
            lock (Padlock)
            {
                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("DELETE FROM " + Core.Db.Approver.DOCUMENT_NAME
                    + " WHERE " + Approver.COLUMN_NAME_USER_ID + " = " + int.Parse(userId) + ";", conn);
                _ = command.ExecuteNonQuery();
            }
        }

        public override IEnumerable<Core.Db.Approver> GetApprovers(string recordId)
        {
            lock (Padlock)
            {
                List<Approver> approvers = new();

                using MySqlConnection conn = new(_connectionString);
                conn.Open();

                using MySqlCommand command = new("SELECT "
                                                 + Approver.COLUMN_NAME_ID + ", "
                                                 + Approver.COLUMN_NAME_USER_ID + ", "
                                                 + Approver.COLUMN_NAME_RECORD_ID + ", "
                                                 + Approver.COLUMN_NAME_APPROVED + ", "
                                                 + Approver.COLUMN_NAME_APPROVED_ON
                                                 + " FROM " + Core.Db.Approver.DOCUMENT_NAME
                                                 + " WHERE " + Approver.COLUMN_NAME_RECORD_ID + " = " + int.Parse(recordId)
                                                 + ";", conn);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    Approver approver = new()
                    {
                        Id = (int)reader[Approver.COLUMN_NAME_ID],
                        UserId = ((int)reader[Approver.COLUMN_NAME_USER_ID]).ToString(),
                        RecordId = ((int)reader[Approver.COLUMN_NAME_RECORD_ID]).ToString(),
                        Approved = (ulong)reader[Approver.COLUMN_NAME_APPROVED] == 1,
                        ApprovedOn = reader[Approver.COLUMN_NAME_APPROVED_ON] == DBNull.Value ? null : (DateTime?)reader[Approver.COLUMN_NAME_APPROVED_ON]
                    };

                    approvers.Add(approver);
                }

                return approvers;
            }
        }

        public override void Dispose()
        {
        }
    }
}