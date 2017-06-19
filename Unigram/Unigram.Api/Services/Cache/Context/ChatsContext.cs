﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Universal.WinSQLite;

namespace Telegram.Api.Services.Cache.Context
{
    public class ChatsContext : Context<TLChatBase>
    {
        private readonly string _fields = "`id`,`access_hash`,`flags`,`title`,`username`,`version`,`participants_count`,`date`,`restriction_reason`,`photo_small_local_id`,`photo_small_secret`,`photo_small_volume_id`,`photo_small_dc_id`,`photo_big_local_id`,`photo_big_secret`,`photo_big_volume_id`,`photo_big_dc_id`,`migrated_to_id`,`migrated_to_access_hash`,`type`";
        private readonly Database _database;

        public ChatsContext(Database database)
        {
            _database = database;
        }

        public IDisposable Transaction()
        {
            return new DatabaseTransaction(_database);
        }

        public override TLChatBase this[long index]
        {
            get
            {
                if (TryGetValue(index, out TLChatBase value))
                {
                    return value;
                }

                Statement statement;
                Sqlite3.sqlite3_prepare_v2(_database, $"SELECT {_fields} FROM `Chats` WHERE `id` = {index}", out statement);

                TLChatBase result = null;
                if (Sqlite3.sqlite3_step(statement) == SQLiteResult.Row)
                {
                    var id = Sqlite3.sqlite3_column_int(statement, 0);
                    var title = Sqlite3.sqlite3_column_text(statement, 3);
                    var version = Sqlite3.sqlite3_column_int(statement, 5);
                    var date = Sqlite3.sqlite3_column_int(statement, 7);

                    TLChatPhotoBase photo = null;
                    var photoType = Sqlite3.sqlite3_column_type(statement, 9);
                    if (photoType == 1) // SQLITE_INTEGER
                    {
                        var photo_small_local_id = Sqlite3.sqlite3_column_int(statement, 9);
                        var photo_small_secret = Sqlite3.sqlite3_column_int64(statement, 10);
                        var photo_small_volume_id = Sqlite3.sqlite3_column_int64(statement, 11);
                        var photo_small_dc_id = Sqlite3.sqlite3_column_int(statement, 12);

                        var photo_big_local_id = Sqlite3.sqlite3_column_int(statement, 13);
                        var photo_big_secret = Sqlite3.sqlite3_column_int64(statement, 14);
                        var photo_big_volume_id = Sqlite3.sqlite3_column_int64(statement, 15);
                        var photo_big_dc_id = Sqlite3.sqlite3_column_int(statement, 16);

                        photo = new TLChatPhoto
                        {
                            PhotoSmall = new TLFileLocation
                            {
                                LocalId = photo_small_local_id,
                                Secret = photo_small_secret,
                                VolumeId = photo_small_volume_id,
                                DCId = photo_small_dc_id
                            },
                            PhotoBig = new TLFileLocation
                            {
                                LocalId = photo_big_local_id,
                                Secret = photo_big_secret,
                                VolumeId = photo_big_volume_id,
                                DCId = photo_big_dc_id
                            }
                        };
                    }
                    else
                    {
                        photo = new TLChatPhotoEmpty();
                    }

                    var type = Sqlite3.sqlite3_column_int(statement, 19);
                    if (type == 0) // CHAT
                    {
                        var flags = (TLChat.Flag)Sqlite3.sqlite3_column_int(statement, 2);
                        var participants_count = Sqlite3.sqlite3_column_int(statement, 6);

                        TLInputChannelBase migratedTo = null;
                        if (flags.HasFlag(TLChat.Flag.MigratedTo))
                        {
                            var channel_id = Sqlite3.sqlite3_column_int(statement, 17);
                            var access_hash = Sqlite3.sqlite3_column_int64(statement, 18);

                            migratedTo = new TLInputChannel { ChannelId = channel_id, AccessHash = access_hash };
                        }

                        result = new TLChat
                        {
                            Id = id,
                            Flags = flags,
                            Title = title,
                            Version = version,
                            ParticipantsCount = participants_count,
                            Date = date,
                            Photo = photo,
                            MigratedTo = migratedTo
                        };
                    }
                    else
                    {
                        var flags = (TLChannel.Flag)Sqlite3.sqlite3_column_int(statement, 2);
                        var access_hash = Sqlite3.sqlite3_column_int64(statement, 1);
                        var username = Sqlite3.sqlite3_column_text(statement, 4);
                        var restriction_reason = Sqlite3.sqlite3_column_text(statement, 8);

                        result = new TLChannel
                        {
                            Id = id,
                            AccessHash = access_hash,
                            Flags = flags,
                            Title = title,
                            Username = username,
                            Version = version,
                            Date = date,
                            RestrictionReason = restriction_reason,
                            Photo = photo
                        };
                    }

                    base[index] = result;
                }

                Sqlite3.sqlite3_finalize(statement);
                return result;
            }
            set
            {
                base[index] = value;

                Statement statement;
                Sqlite3.sqlite3_prepare_v2(_database, $"INSERT OR REPLACE INTO `Chats` ({_fields}) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)", out statement);

                if (value is TLChat chat)
                {
                    Sqlite3.sqlite3_bind_int64(statement, 1, chat.Id);
                    Sqlite3.sqlite3_bind_null(statement, 2);
                    Sqlite3.sqlite3_bind_int(statement, 3, (int)chat.Flags);
                    Sqlite3.sqlite3_bind_text(statement, 4, chat.Title, -1);
                    Sqlite3.sqlite3_bind_null(statement, 5);
                    Sqlite3.sqlite3_bind_int(statement, 6, chat.Version);
                    Sqlite3.sqlite3_bind_int(statement, 7, chat.ParticipantsCount);
                    Sqlite3.sqlite3_bind_int(statement, 8, chat.Date);
                    Sqlite3.sqlite3_bind_null(statement, 9);

                    if (chat.Photo is TLChatPhoto photo && photo.PhotoSmall is TLFileLocation small && photo.PhotoBig is TLFileLocation big)
                    {
                        Sqlite3.sqlite3_bind_int(statement, 10, small.LocalId);
                        Sqlite3.sqlite3_bind_int64(statement, 11, small.Secret);
                        Sqlite3.sqlite3_bind_int64(statement, 12, small.VolumeId);
                        Sqlite3.sqlite3_bind_int(statement, 13, small.DCId);
                        Sqlite3.sqlite3_bind_int(statement, 14, big.LocalId);
                        Sqlite3.sqlite3_bind_int64(statement, 15, big.Secret);
                        Sqlite3.sqlite3_bind_int64(statement, 16, big.VolumeId);
                        Sqlite3.sqlite3_bind_int(statement, 17, big.DCId);
                    }
                    else
                    {
                        Sqlite3.sqlite3_bind_null(statement, 10);
                        Sqlite3.sqlite3_bind_null(statement, 11);
                        Sqlite3.sqlite3_bind_null(statement, 12);
                        Sqlite3.sqlite3_bind_null(statement, 13);
                        Sqlite3.sqlite3_bind_null(statement, 14);
                        Sqlite3.sqlite3_bind_null(statement, 15);
                        Sqlite3.sqlite3_bind_null(statement, 16);
                        Sqlite3.sqlite3_bind_null(statement, 17);
                    }

                    if (chat.HasMigratedTo && chat.MigratedTo is TLInputChannel inputChannel)
                    {
                        Sqlite3.sqlite3_bind_int(statement, 18, inputChannel.ChannelId);
                        Sqlite3.sqlite3_bind_int64(statement, 19, inputChannel.AccessHash);
                    }
                    else
                    {
                        Sqlite3.sqlite3_bind_null(statement, 18);
                        Sqlite3.sqlite3_bind_null(statement, 19);
                    }

                    Sqlite3.sqlite3_bind_int(statement, 20, 0);
                }
                else if (value is TLChannel channel)
                {
                    Sqlite3.sqlite3_bind_int64(statement, 1, channel.Id);

                    if (channel.HasAccessHash)
                    {
                        Sqlite3.sqlite3_bind_int64(statement, 2, channel.AccessHash.Value);
                    }
                    else
                    {
                        Sqlite3.sqlite3_bind_null(statement, 2);
                    }

                    Sqlite3.sqlite3_bind_int(statement, 3, (int)channel.Flags);
                    Sqlite3.sqlite3_bind_text(statement, 4, channel.Title, -1);

                    if (channel.HasUsername)
                    {
                        Sqlite3.sqlite3_bind_text(statement, 5, channel.Username, -1);
                    }
                    else
                    {
                        Sqlite3.sqlite3_bind_null(statement, 5);
                    }

                    Sqlite3.sqlite3_bind_int(statement, 6, channel.Version);
                    Sqlite3.sqlite3_bind_null(statement, 7);
                    Sqlite3.sqlite3_bind_int(statement, 8, channel.Date);
                    Sqlite3.sqlite3_bind_null(statement, 9);

                    if (channel.Photo is TLChatPhoto photo && photo.PhotoSmall is TLFileLocation small && photo.PhotoBig is TLFileLocation big)
                    {
                        Sqlite3.sqlite3_bind_int(statement, 10, small.LocalId);
                        Sqlite3.sqlite3_bind_int64(statement, 11, small.Secret);
                        Sqlite3.sqlite3_bind_int64(statement, 12, small.VolumeId);
                        Sqlite3.sqlite3_bind_int(statement, 13, small.DCId);
                        Sqlite3.sqlite3_bind_int(statement, 14, big.LocalId);
                        Sqlite3.sqlite3_bind_int64(statement, 15, big.Secret);
                        Sqlite3.sqlite3_bind_int64(statement, 16, big.VolumeId);
                        Sqlite3.sqlite3_bind_int(statement, 17, big.DCId);
                    }
                    else
                    {
                        Sqlite3.sqlite3_bind_null(statement, 10);
                        Sqlite3.sqlite3_bind_null(statement, 11);
                        Sqlite3.sqlite3_bind_null(statement, 12);
                        Sqlite3.sqlite3_bind_null(statement, 13);
                        Sqlite3.sqlite3_bind_null(statement, 14);
                        Sqlite3.sqlite3_bind_null(statement, 15);
                        Sqlite3.sqlite3_bind_null(statement, 16);
                        Sqlite3.sqlite3_bind_null(statement, 17);
                    }

                    Sqlite3.sqlite3_bind_null(statement, 18);
                    Sqlite3.sqlite3_bind_null(statement, 19);

                    Sqlite3.sqlite3_bind_int(statement, 20, 1);
                }

                Sqlite3.sqlite3_step(statement);
                Sqlite3.sqlite3_reset(statement);

                Sqlite3.sqlite3_finalize(statement);
            }
        }
    }

    public class DatabaseTransaction : IDisposable
    {
        private readonly Database _database;

        public DatabaseTransaction(Database database)
        {
            _database = database;

            Statement statement;
            Sqlite3.sqlite3_prepare_v2(_database, "BEGIN IMMEDIATE TRANSACTION", out statement);
            Sqlite3.sqlite3_step(statement);
            Sqlite3.sqlite3_finalize(statement);
        }

        public void Dispose()
        {
            Statement statement;
            Sqlite3.sqlite3_prepare_v2(_database, "COMMIT TRANSACTION", out statement);
            Sqlite3.sqlite3_step(statement);
            Sqlite3.sqlite3_finalize(statement);
        }
    }
}
