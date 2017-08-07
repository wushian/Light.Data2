﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Light.Data.Test
{
    public abstract class BaseTest
    {
        static BaseTest()
        {

            DataMapperConfiguration.AddConfigFilePath("Config/mapper.json");
            DataMapperConfiguration.AddConfigFilePath("Config/mapper_relate.json");
        }

        readonly protected DataContext context = null;

        readonly protected CommandOutput commandOutput = new CommandOutput();

        public const double DELTA = 0.0001;

        protected readonly ITestOutputHelper output;

        protected BaseTest(ITestOutputHelper output)
        {
            context = new DataContext("mssql");
            commandOutput.OutputFullCommand = true;
            commandOutput.OnCommandOutput += CommandOutput_OnCommandOutput;
            //output.UseConsoleOutput = true;
            context.SetCommanfOutput(commandOutput);
            this.output = output;

        }

        public DataContext CreateContext()
        {
            DataContext context = new DataContext("mssql");
            context.SetCommanfOutput(commandOutput);
            return context;
        }

        public DataContext CreateContext(string configName)
        {
            DataContext context = new DataContext(configName);
            context.SetCommanfOutput(commandOutput);
            return context;
        }

        private void CommandOutput_OnCommandOutput(object sender, CommandOutputEventArgs args)
        {
            this.output.WriteLine(args.RunnableCommand);
        }

        protected DateTime GetNow()
        {
            DateTime now = DateTime.Now;
            DateTime d = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
            return d;
        }
    }
}
