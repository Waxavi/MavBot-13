using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class MavBot13 : Robot
    {
        [Parameter("Lot Size", DefaultValue = 0.01)]
        public double _LotSize { get; set; }
        [Parameter("SL", DefaultValue = 10)]
        public double _SL { get; set; }
        [Parameter("TP", DefaultValue = 10)]
        public double _TP { get; set; }
        [Parameter("Stochastics", DefaultValue = "----------")]
        public string _StringStochastics { get; set; }
        [Parameter("SO K Periods", DefaultValue = 9)]
        public int _SOKP { get; set; }
        [Parameter("SO D Periods", DefaultValue = 9)]
        public int _SODP { get; set; }
        [Parameter("SO Slowing", DefaultValue = 3)]
        public int _SOKS { get; set; }
        [Parameter("SO Overbought", DefaultValue = 80)]
        public double _SOOB { get; set; }
        [Parameter("SO Oversold", DefaultValue = 20)]
        public double _SOOS { get; set; }
        [Parameter("ATR", DefaultValue = "----------")]
        public string _StringATR { get; set; }
        [Parameter("ATR Periods", DefaultValue = 14)]
        public int _ATRPeriods { get; set; }
        [Parameter("ATR Filter Value (avg pips)", DefaultValue = 10)]
        public double _ATRFilterValue { get; set; }

        private string _Label;
        private StochasticOscillator _SO;
        private AverageTrueRange _ATR;

        private double _ATRinPips
        {
            get { return _ATR.Result.LastValue / Symbol.PipSize; }
        }

        private bool ZeroPos
        {
            get { return Positions.Count(item => item.Label == _Label) == 0; }
        }

        private DateTime LastTradeEntryTime
        {
            get
            {
                if (History.Count(item => item.Label == _Label) == 0)
                {
                    return DateTime.MinValue;
                }
                else
                {
                    return History.Where(item => item.Label == _Label).Max(item => item.EntryTime);
                }
            }
        }

        private DateTime? SignalStartTime
        {
            get
            {
                if (!Signal().HasValue)
                {
                    return null;
                }
                else
                {
                    int i = 0;
                    if (Signal().Value)
                    {
                        while (Signal(i).HasValue && Signal(i).Value)
                        {
                            i++;
                        }

                        return MarketSeries.OpenTime.Last(i);
                    }
                    else
                    {
                        while (Signal(i).HasValue && !Signal(i).Value)
                        {
                            i++;
                        }

                        return MarketSeries.OpenTime.Last(i);
                    }
                }
            }
        }

        private bool? Signal(int i = 0)
        {
            if (_SO.PercentK.Last(i) < _SOOS)
            {
                return true;
            }
            else if (_SO.PercentK.Last(i) > _SOOB)
            {
                return false;
            }
            else
            {
                return null;
            }
        }

        private void ExecuteTrade(TradeType _tt, string _message = "Order Executed.")
        {
            TradeResult _TR = ExecuteMarketOrder(_tt, Symbol, Symbol.NormalizeVolume(Symbol.QuantityToVolume(_LotSize)), _Label, _SL, _TP);
            if (_TR.IsSuccessful)
            {
                Print(_message);
            }
            else
            {
                Print("Error: {0}", _TR.Error);
                Stop();
            }
        }

        protected override void OnStart()
        {
            _Label = Symbol.Code + TimeFrame.ToString() + Server.Time.Ticks.ToString();
            _ATR = Indicators.AverageTrueRange(_ATRPeriods, MovingAverageType.Simple);
            _SO = Indicators.StochasticOscillator(_SOKP, _SOKS, _SODP, MovingAverageType.Simple);

            Print("Bot Rules:");
            Print("Buys or Sell on Stochastic Oscillator Oversold/Overbought parameters filtered by ATR range.");
            Print("*****************************************");
        }


        protected override void OnBar()
        {
            if (ZeroPos)
            {
                try
                {
                    if (_ATRinPips > _ATRFilterValue)
                    {
                        if (Signal().HasValue && SignalStartTime.HasValue)
                        {
                            if (SignalStartTime.Value > LastTradeEntryTime)
                            {
                                if (Signal().Value)
                                {
                                    ExecuteTrade(TradeType.Buy, String.Format("Order Executed | Data for Reference: SO PercentK: {0}, ATR: {1}", Math.Round(_SO.PercentK.LastValue, 2), Math.Round(_ATR.Result.LastValue / Symbol.PipSize, 2)));
                                }
                                else
                                {
                                    ExecuteTrade(TradeType.Sell, String.Format("Order Executed | Data for Reference: SO PercentK: {0}, ATR: {1}", Math.Round(_SO.PercentK.LastValue, 2), Math.Round(_ATR.Result.LastValue / Symbol.PipSize, 2)));
                                }
                            }
                        }
                    }
                } catch (Exception ex)
                {
                    Print(ex.InnerException);
                    Print(ex.Message);
                    Print(ex.Source);
                    Print(ex.StackTrace);
                    Print(ex.TargetSite);
                    Stop();
                    throw;
                }
            }
        }

        protected override void OnTick()
        {
            // Put your core logic here
        }

        protected override void OnStop()
        {
            if (IsBacktesting)
            {
                if (Positions.Count > 0)
                {
                    foreach (var pos in Positions)
                    {
                        ClosePosition(pos);
                    }
                }

                if (PendingOrders.Count > 0)
                {
                    foreach (var pen in PendingOrders)
                    {
                        CancelPendingOrder(pen);
                    }
                }
            }
        }
    }
}
