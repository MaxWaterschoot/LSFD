using System.Collections.Generic;
using System.Linq;
using LSFD.API;
using LSFD.Apparatus;
using LSFD.Core;
using LSFD.Firefighting;
using LSFD.Profiles;
using LSFD.Safety;
using LSFD.VehicleRescue;
using LSFD.Water;
using RAGENativeUI;
using RAGENativeUI.Elements;
using Rage;

namespace LSFD.UI
{
    /// <summary>On-screen info: tank, nozzle, tool and progress timer bars, hose controls and big messages.</summary>
    internal class Hud
    {
        private const float LowWaterFraction = 0.2f;
        private const float FireStatusRange = 120f;

        private readonly TimerBarPool timerBars = new TimerBarPool();
        private readonly List<TimerBarBase> shownBars = new List<TimerBarBase>();

        private readonly BarTimerBar tankBar = new BarTimerBar(L.T("TANK")) { BackgroundColor = Theme.BarBackground };
        private readonly TextTimerBar supplyBar = new TextTimerBar(L.T("WATER"), "");
        private readonly TextTimerBar nozzleBar = new TextTimerBar(L.T("NOZZLE"), "");
        private readonly TextTimerBar foamBar = new TextTimerBar(L.T("FOAM"), "");
        private readonly BarTimerBar pressureBar = new BarTimerBar(L.T("PRESSURE")) { ForegroundColor = Theme.Highlight, BackgroundColor = Theme.BarBackground };
        private readonly TextTimerBar toolBar = new TextTimerBar(L.T("TOOL"), "");
        private readonly TextTimerBar fireBar = new TextTimerBar(L.T("FIRE"), "");
        private readonly BarTimerBar airBar = new BarTimerBar(L.T("AIR")) { BackgroundColor = Theme.BarBackground };
        private readonly TextTimerBar heatBar = new TextTimerBar(L.T("HEAT"), L.T("~r~DANGER"));
        private readonly TextTimerBar cameraBar = new TextTimerBar(L.T("TIC"), "");
        private readonly BarTimerBar progressBar = new BarTimerBar("") { ForegroundColor = Theme.Highlight, BackgroundColor = Theme.BarBackground };

        private readonly InstructionalButtons hoseButtons = new InstructionalButtons();

        private readonly BigMessageThread bigMessage = new BigMessageThread();

        public Hud()
        {
            hoseButtons.Buttons.Add(new InstructionalButton(GameControl.Attack, "Spray"));
            hoseButtons.Buttons.Add(new InstructionalButtonGroup("Pattern", Theme.ButtonFor(Settings.NozzlePatternKey)));
            hoseButtons.Buttons.Add(new InstructionalButtonGroup("Water/foam", Theme.ButtonFor(Settings.FoamKey)));
            hoseButtons.Buttons.Add(new InstructionalButtonGroup("Pressure", Theme.ButtonFor(Settings.PressureDownKey), Theme.ButtonFor(Settings.PressureUpKey)));
            hoseButtons.Update();
        }

        public void Process(HoseManager hose, HydrantManager hydrants, ApparatusManager apparatus, ToolManager tools, InteractionRunner interactions,
            FireSystem fire, BreathingApparatus scba, HazardManager hazards, ThermalCamera camera, bool onDuty)
        {
            var bars = new List<TimerBarBase>();
            Vector3 player = Game.LocalPlayer.Character.Position;

            FireIncident incident = fire.FindNearest(player, FireStatusRange);
            if (incident != null)
            {
                fireBar.Text = $"{incident.Status.ToDisplayName()}~s~ · {CalloutPriorityExtensions.AlarmToDisplay(incident.AlarmLevel)}";
                bars.Add(fireBar);
            }

            // The cylinder only matters once it is in use or no longer full.
            if (onDuty && (scba.IsMaskOn || scba.AirFraction < 0.999f || hazards.Smoke > 0f))
            {
                airBar.Label = scba.IsMaskOn ? L.T("AIR") : L.T("AIR (MASK OFF)");
                airBar.Percentage = scba.AirFraction;
                airBar.ForegroundColor = scba.IsLowOnAir ? Theme.WaterLow : Theme.Water;
                bars.Add(airBar);
            }

            if (hazards.IsInDangerousHeat)
                bars.Add(heatBar);

            if (camera.IsActive)
            {
                cameraBar.Text = (camera.VisibleVictims == 1 ? L.T("1 victim") : L.F($"{camera.VisibleVictims} victims")) + " · " +
                    (camera.VisibleHotspots == 1 ? L.T("1 hotspot") : L.F($"{camera.VisibleHotspots} hotspots"));
                bars.Add(cameraBar);
            }

            FireApparatus focus = hose.Source ?? hydrants.CarryingFrom ?? apparatus.Current;
            if (focus != null && focus.Definition.HasPump)
            {
                tankBar.Percentage = focus.WaterFraction;
                tankBar.ForegroundColor = focus.WaterFraction < LowWaterFraction && !focus.IsSupplied ? Theme.WaterLow : Theme.Water;
                supplyBar.Text = focus.IsSupplied ? L.T("~b~HYDRANT") : $"{focus.Water:N0} L";
                bars.Add(supplyBar);
                bars.Add(tankBar);
            }

            if (hose.IsHolding)
            {
                nozzleBar.Text = (hose.Nozzle.IsStraight ? L.T("STRAIGHT") : L.T("FOG")) + (hose.Nozzle.IsFoam ? L.T(" · ~w~FOAM") : "");
                pressureBar.Percentage = hose.Nozzle.EffectivePressure;
                bars.Add(nozzleBar);
                if (hose.Source.Definition.FoamCapacity > 0f)
                {
                    foamBar.Text = $"{hose.Source.Foam:N0} L";
                    bars.Add(foamBar);
                }
                bars.Add(pressureBar);
            }

            if (tools.Held != null)
            {
                toolBar.Text = tools.Held.Type.ToDisplayName().ToUpperInvariant();
                bars.Add(toolBar);
            }

            if (interactions.Progress.HasValue)
            {
                progressBar.Label = interactions.ProgressLabel;
                progressBar.Percentage = interactions.Progress.Value;
                bars.Add(progressBar);
            }

            ShowBars(bars);
            timerBars.Draw();

            if (hose.IsHolding)
                hoseButtons.Draw();
        }

        /// <summary>The report after a call: a big message with the score and the details in a notification.</summary>
        public void ShowCallReport(CallRecord report)
        {
            string title = report.Completed ? L.T("CALL COMPLETED") : L.T("CALL ENDED");
            bigMessage.MessageInstance.ShowColoredShard(title, L.F($"{report.Name} · Score {report.Score} ({CallScore.Grade(report.Score)})"),
                report.Completed ? HudColor.Red : HudColor.Black, HudColor.Black, 5000);

            int points = CallScore.Points(report);
            Notify.Dispatch(L.F($"Call report: {report.Name}"),
                L.F($"Response: {CallScore.Response(report)}~n~Water: ~b~{report.WaterUsed:N0} L~s~ · On the call {report.Minutes} min~n~Rescued ~g~{report.Rescued}~s~ · Treated {report.Treated}") + (report.Deceased > 0 ? L.F($" · Lost ~r~{report.Deceased}~s~") : "") + L.F($"~n~Score: {CallScore.Display(report.Score)}") + (points > 0 ? L.F($" · ~y~+{points}~s~ points") : ""));
        }

        public void ShowPromotion(string rank)
        {
            bigMessage.MessageInstance.ShowColoredShard("PROMOTED", rank, HudColor.Gold, HudColor.Black, 5000);
        }

        public void ShowDutyChanged(bool onDuty)
        {
            if (onDuty)
                bigMessage.MessageInstance.ShowColoredShard(L.T("ON DUTY"), L.T("Los Santos Fire Department"), HudColor.Red, HudColor.Black, 4000);
            else
                bigMessage.MessageInstance.ShowColoredShard(L.T("OFF DUTY"), L.T("Thanks for your service"), HudColor.Black, HudColor.Red, 3000);
        }

        private void ShowBars(List<TimerBarBase> bars)
        {
            if (bars.SequenceEqual(shownBars))
                return;

            foreach (TimerBarBase bar in shownBars)
                timerBars.Remove(bar);
            foreach (TimerBarBase bar in bars)
                timerBars.Add(bar);

            shownBars.Clear();
            shownBars.AddRange(bars);
        }
    }
}
