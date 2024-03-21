using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using Serilog;
using zzio;
using zzio.vfs;
using zzre.game;

namespace zzre;

partial class Program
{
    private static readonly Option<bool> OptionNoZanzarahConfig = new(
        "--no-zanzarah-config",
        () => false,
        "Prevents the loading of standard zanzarah config files (game.cfg, ai.cfg, net.cfg, wizform.cfg)");

    private static readonly Option<string[]> OptionSingleConfigs = new(
        new[] { "-c", "--config" },
        () => [],
        "Sets a single configuration value in the form <name>=<value>");

    private static void AddConfigurationOptions(Command command)
    {
        command.AddGlobalOption(OptionNoZanzarahConfig);
        command.AddGlobalOption(OptionSingleConfigs);
    }

    private static Configuration CreateConfiguration(ITagContainer diContainer)
    {
        var invocationCtx = diContainer.GetTag<InvocationContext>();
        var logger = diContainer.GetLoggerFor<Configuration>();

        var config = new Configuration();
        config.AddSource(GameConfigSource.Default);
        config.AddSource(new ConfigurationSectionAsSource("Default TestDuel", new TestDuelConfig()));
        if (!invocationCtx.ParseResult.GetValueForOption(OptionNoZanzarahConfig))
        {
            LoadGameConfig(diContainer, config, "Configs/game.cfg");
            LoadVarConfig(diContainer, config, "System/ai.cfg", "zanzarah.ai");
            LoadVarConfig(diContainer, config, "System/net.cfg", "zanzarah.net");
            LoadVarConfig(diContainer, config, "System/wizform.cfg", "zanzarah.wizform");
        }
        else
            logger.Debug("Loading of Zanzarah standard config files disabled by command line");

        var singleConfigs = invocationCtx.ParseResult.GetValueForOption(OptionSingleConfigs) ?? [];
        foreach (var singleConfig in singleConfigs)
            AddSingleConfig(logger, config, singleConfig);

        config.ApplyChanges();
        return config;
    }

    private static void LoadGameConfig(ITagContainer diContainer, Configuration config, string path)
    {
        try
        {
            var resourcePool = diContainer.GetTag<IResourcePool>();
            using var stream = resourcePool.FindAndOpen(path) ?? throw new FileNotFoundException();
            var gameConfig = GameConfig.ReadNew(stream);
            config.AddSource(GameConfigSource.Create(gameConfig));
        }
        catch(Exception e)
        {
            diContainer.GetLoggerFor<Configuration>().Error("GameConfig {Path}: {Error}", path, e.Message);
        }
    }

    private static void LoadVarConfig(ITagContainer diContainer, Configuration config, string path, string section)
    {
        try
        {
            var resourcePool = diContainer.GetTag<IResourcePool>();
            using var stream = resourcePool.FindAndOpen(path) ?? throw new FileNotFoundException();
            var varConfig = VarConfig.ReadNew(stream);
            var name = $"VarConfig {Path.GetFileNameWithoutExtension(path)}";
            config.AddSource(new VarConfigurationSource(name, varConfig, section));
        }
        catch(Exception e)
        {
            diContainer.GetLoggerFor<Configuration>().Error("VarConfig {Path}: {Error}", path, e.Message);
        }
    }

    private static void AddSingleConfig(ILogger logger, Configuration config, string spec)
    {
        var parts = spec.Split('=');
        if (parts.Length == 2)
        {
            parts[0] = parts[0].Trim();
            parts[1] = parts[1].Trim();
            if (parts[0].Length > 0 && parts[1].Length > 0)
            {
                if (double.TryParse(parts[1], out var numeric))
                    config.SetValue(parts[0], numeric);
                else
                    config.SetValue(parts[0], parts[1]);
                return;
            }
        }
        logger.Warning("Did not understand single config spec: {Spec}", spec);
    }

    public static IConfigurationBinding GetConfigFor<TSection>(this ITagContainer diContainer, TSection instance)
        where TSection : class, IConfigurationSection
        => diContainer.GetTag<Configuration>().Bind(instance);
}
