﻿using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using PluginCore.IPlugins;
using Konata.Core;
using Konata.Core.Events.Model;
using Konata.Core.Interfaces.Api;
using QQBotHub.Sdk.IPlugins;
using QQStatPlugin.Utils;
using Konata.Core.Message.Model;
using Konata.Core.Common;
using System.Text;

namespace QQStatPlugin
{
    public class QQStatPlugin : BasePlugin, IQQBotPlugin
    {
        public override (bool IsSuccess, string Message) AfterEnable()
        {
            Console.WriteLine($"{nameof(QQStatPlugin)}: {nameof(AfterEnable)}");
            return base.AfterEnable();
        }

        public override (bool IsSuccess, string Message) BeforeDisable()
        {
            Console.WriteLine($"{nameof(QQStatPlugin)}: {nameof(BeforeDisable)}");
            return base.BeforeDisable();
        }

        #region QQBot
        public void OnGroupMessage((Bot s, GroupMessageEvent e) obj, string message, string groupName, uint groupUin, uint memberUin)
        {
            SettingsModel settingsModel = PluginCore.PluginSettingsModelFactory.Create<SettingsModel>(nameof(QQStatPlugin));

            #region 收集群消息
            try
            {
                // 保存数据库
                int successRow = DbContext.InsertIntoMessage(new Models.Message()
                {
                    Content = message,
                    CreateTime = DateTime.Now.ToTimeStamp13(),
                    GroupName = groupName,
                    GroupUin = groupUin.ToString(),
                    QQName = obj.e.Message.Sender.Name,
                    QQUin = memberUin.ToString()
                });
                Console.WriteLine($"成功插入 {successRow} 行");
            }
            catch (Exception ex)
            {
                Console.WriteLine("插入表 Message 出错:");
                Console.WriteLine(ex.ToString());
            }
            #endregion

            #region 图表
            Console.WriteLine($"{groupName} ({groupUin})");
            bool isChartGroup = settingsModel.ChartGroups != null && settingsModel.ChartGroups.Count >= 1 && settingsModel.ChartGroups.Contains(groupUin.ToString());
            bool isAdminGroup = settingsModel.AdminGroups != null && settingsModel.AdminGroups.Count >= 1 && settingsModel.AdminGroups.Contains(groupUin.ToString());
            Console.WriteLine($"{nameof(isChartGroup)}:{isChartGroup}");
            Console.WriteLine($"{nameof(isAdminGroup)}:{isAdminGroup}");
            if (isAdminGroup || isChartGroup)
            {
                Console.WriteLine($"进入 {DateTime.Now.ToString()}");
                BotMember member = null;
                try
                {
                    member = obj.s.GetGroupMemberInfo(groupUin: groupUin, memberUin: memberUin, forceUpdate: true).Result;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("获取群成员信息失败");
                }
                if (member != null)
                {
                    bool isAdmin = member.Uin.ToString() == settingsModel.AdminQQ || member.Role == Konata.Core.Common.RoleType.Owner || member.Role == Konata.Core.Common.RoleType.Admin;
                    if (isAdmin || isChartGroup)
                    {
                        if (message.Contains("#帮助"))
                        {
                            StringBuilder stringBuilder = new StringBuilder();
                            stringBuilder.AppendLine("#日历");
                            stringBuilder.AppendLine("#日历 指定某人QQ");
                            stringBuilder.AppendLine("#折线");
                            stringBuilder.AppendLine("#折线 指定某人QQ");
                            obj.s.SendGroupMessage(groupUin, stringBuilder.ToString());
                        }
                        else if (message.Contains("#日历"))
                        {
                            #region 日历
                            SendCalendar(obj, message, groupUin, memberUin, settingsModel);
                            #endregion
                        }
                        else if (message.Contains("#折线"))
                        {
                            #region 折线
                            SendStackedArea(obj, message, groupUin, memberUin, settingsModel);
                            #endregion
                        }
                    }
                }
            }
            #endregion

        }




        public void OnFriendMessage((Bot s, FriendMessageEvent e) obj, string message, uint friendUin)
        {
            // 保存数据库
            #region 只收集群聊
            //try
            //{
            //    int successRow = DbContext.InsertIntoMessage(new Models.Message()
            //    {
            //        Content = message,
            //        CreateTime = DateTime.Now.ToTimeStamp13(),
            //        GroupName = null,
            //        GroupUin = null,
            //        QQName = obj.e.Message.Sender.Name,
            //        QQUin = friendUin.ToString()
            //    });
            //    Console.WriteLine($"成功插入 {successRow} 行");
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine("插入表 Message 出错:");
            //    Console.WriteLine(ex.ToString());
            //} 
            #endregion

            SettingsModel settingsModel = PluginCore.PluginSettingsModelFactory.Create<SettingsModel>(nameof(QQStatPlugin));
            if (settingsModel.AdminQQ != null && settingsModel.AdminQQ == friendUin.ToString())
            {
                // 来自 超级管理员 的消息
                if (message.Trim() == "#计数")
                {
                    try
                    {
                        var messages = DbContext.QueryAllMessage();
                        obj.s.SendFriendMessage(friendUin, $"共收集 Message {(messages?.Count.ToString() ?? "0")} 条");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("查询 Message 出错");
                    }
                }
            }


        }

        public void OnBotOnline((Bot s, BotOnlineEvent e) obj, string botName, uint botUin)
        {
            //SettingsModel settingsModel = PluginCore.PluginSettingsModelFactory.Create<SettingsModel>(nameof(QQStatPlugin));

            //if (settingsModel != null && !string.IsNullOrEmpty(settingsModel.AdminQQ))
            //{
            //    obj.s.SendFriendMessage(Convert.ToUInt32(settingsModel.AdminQQ), $"{obj.s.Name}({obj.s.Uin}) 上线啦");
            //}
        }

        public void OnBotOffline((Bot s, BotOfflineEvent e) obj, string botName, uint botUin)
        {
            //SettingsModel settingsModel = PluginCore.PluginSettingsModelFactory.Create<SettingsModel>(nameof(QQStatPlugin));

            //if (settingsModel != null && !string.IsNullOrEmpty(settingsModel.AdminQQ))
            //{
            //    obj.s.SendFriendMessage(Convert.ToUInt32(settingsModel.AdminQQ), $"{obj.s.Name}({obj.s.Uin}) 离线啦");
            //}
        }
        #endregion


        public void SendCalendar((Bot s, GroupMessageEvent e) obj, string message, uint groupUin, uint memberUin, SettingsModel settingsModel)
        {
            string token = Guid.NewGuid().ToString();
            Controllers.CalendarController.CreateTime = DateTime.Now;

            // 下方获取当前群聊
            string urlParam = $"{settingsModel.BaseUrl}/Plugins/QQStatPlugin/Calendar?groupUin={groupUin.ToString()}";
            string targetMemberUinStr = message.Replace("#日历", "")?.Trim();
            if (uint.TryParse(targetMemberUinStr, out uint targetMemberUin))
            {
                // 仅此人 日历
                urlParam += $"&memeberUin={targetMemberUin}";
            }
            // 注意: url 编码, 这样才能正确传参
            urlParam = System.Web.HttpUtility.UrlEncode(urlParam, System.Text.Encoding.UTF8);
            // 加个time 防止缓存
            // ScreenshotUrl: xxx.com?url=
            string imageUrl = $"{settingsModel.ScreenshotUrl}{urlParam}&time={DateTime.Now.ToTimeStamp13()}";

            Console.WriteLine(imageUrl);
            try
            {
                var image = ImageChain.CreateFromUrl(imageUrl);
                obj.s.SendGroupMessage(groupUin, image);
                obj.s.SendGroupPoke(groupUin: groupUin, memberUin: memberUin);
                obj.s.SendGroupMessage(groupUin, AtChain.Create(memberUin));
            }
            catch (Exception ex)
            {
                obj.s.SendGroupMessage(groupUin, "发送 日历 图片失败");
                //obj.s.SendGroupMessage(groupUin, imageUrl);

                Console.WriteLine("发送 日历 图片失败");
                Console.WriteLine(ex.ToString());
            }
        }

        public void SendStackedArea((Bot s, GroupMessageEvent e) obj, string message, uint groupUin, uint memberUin, SettingsModel settingsModel)
        {
            string token = Guid.NewGuid().ToString();
            Controllers.StackedAreaController.CreateTime = DateTime.Now;

            // 下方获取当前群聊
            string urlParam = $"{settingsModel.BaseUrl}/Plugins/QQStatPlugin/StackedArea?groupUin={groupUin.ToString()}";
            string targetMemberUinStr = message.Replace("#折线", "")?.Trim();
            if (uint.TryParse(targetMemberUinStr, out uint targetMemberUin))
            {
                // 仅此人 日历
                urlParam += $"&memeberUin={targetMemberUin}";
            }
            // 注意: url 编码, 这样才能正确传参
            urlParam = System.Web.HttpUtility.UrlEncode(urlParam, System.Text.Encoding.UTF8);
            // 加个time 防止缓存
            // ScreenshotUrl: xxx.com?url=
            string imageUrl = $"{settingsModel.ScreenshotUrl}{urlParam}&time={DateTime.Now.ToTimeStamp13()}";

            Console.WriteLine(imageUrl);
            try
            {
                var image = ImageChain.CreateFromUrl(imageUrl);
                obj.s.SendGroupMessage(groupUin, image);
                obj.s.SendGroupPoke(groupUin: groupUin, memberUin: memberUin);
                obj.s.SendGroupMessage(groupUin, AtChain.Create(memberUin));
            }
            catch (Exception ex)
            {
                obj.s.SendGroupMessage(groupUin, "发送 折线 图片失败");
                //obj.s.SendGroupMessage(groupUin, imageUrl);

                Console.WriteLine("发送 折线 图片失败");
                Console.WriteLine(ex.ToString());
            }
        }

    }
}
