﻿namespace Event_Service.Models.HelperFiles
{
    public static class RabbitMqRouting
    {
        public static readonly string SendMail = "send.mail";
        public static readonly string AddLog = "add.log";
        public static readonly string ConvertDatepicker = "convert.event";
        public static readonly string DeleteUser = "delete.user.event";
    }
}