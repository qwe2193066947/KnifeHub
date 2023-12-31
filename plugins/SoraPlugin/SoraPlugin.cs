﻿using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using PluginCore.IPlugins;
using SoraPlugin.Utils;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace SoraPlugin
{
    public class SoraPlugin : BasePlugin
    {
        public override (bool IsSuccess, string Message) AfterEnable()
        {
            Console.WriteLine($"{nameof(SoraPlugin)}: {nameof(AfterEnable)}");
            return base.AfterEnable();
        }

        public override (bool IsSuccess, string Message) BeforeDisable()
        {
            Console.WriteLine($"{nameof(SoraPlugin)}: {nameof(BeforeDisable)}");
            return base.BeforeDisable();
        }

    }
}
