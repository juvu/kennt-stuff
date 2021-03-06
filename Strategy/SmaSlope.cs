using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms.VisualStyles;
using KenNinja;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Indicator;

namespace NinjaTrader.Custom.Strategy
{
    [Description("Get Some Data")]
    public class SmaSlope : NinjaTrader.Strategy.Strategy
    {
        // User defined variables (add any user defined variables below)

        private const int TrendStrength = 4;
        private static readonly List<Kp> KpsToUse;
        private static int tradeId = 0;
        private SortedList<Guid, ActiveOrder> _activerOrders;
        private int _bears;
        private int _bulls;
        private double _strikeWidth;
        private int _winningBears;
        private int _winningBulls;
        private SortedList<int, double> _smaSlopeHistory;


     


        protected override void Initialize()
        {

            _smaSlopeHistory = new SortedList<int, double>();
            var _binaryWidths = new SortedList<string, double>
            {
                {"$AUDJPY", .05},
                {"$AUDUSD", .0005},
                {"$EURGBP", .00010},
                {"$EURJPY", .1},
                {"$EURUSD", .0004},
                {"$GBPJPY", .1},
                {"$GBPUSD", .0010},
                {"$USDCAD", .0010},
                {"$USDCHF", .0004},
                {"$USDJPY", .04},
            };

            //var  _spreadWidths = new SortedList<string, double> { { "$EURUSD", .0004 }, { "$GBPUSD", .001 }, { "$USDJPY", 1.0 }, { "$AUDUSD", .01 }, { "$USDCAD", .01 }, { "$EURJPY", 1.0 } };


            var _stikeWidths = _binaryWidths;
            var instrument = Instrument.ToString().ToUpper().Replace("DEFAULT", "").Replace(" ", "");
            Print(instrument);
            if (_stikeWidths.ContainsKey(instrument))
            {
                _strikeWidth = _stikeWidths[instrument];
            }
            else
            {
                Print("Fail");
            }


            Log(string.Format("Starting for KenCandleStickStrategy {0}", Instrument), LogLevel.Information);
            // ClearOutputWindow();
            CalculateOnBarClose = true; //only on bar close
            _activerOrders = new SortedList<Guid, ActiveOrder>();
            _bulls = 0;
            _winningBulls = 0;
            _bears = 0;
            _winningBears = 0;
        }


        protected override void OnBarUpdate()
        {

            RecordSmaSlope();

            //bail if we don't have enough bars
            if (this.CurrentBar < 12)
                return;


            try
            {
                HandleCurrentOrders();


                var tickSize = TickSize;


                Print("");

                Print(string.Format("{0} of {1} bulls successful({2})", _winningBulls, _bulls,
                    (_bulls > 0) ? (double)_winningBulls / _bulls : 0));
                Print(string.Format("{0} of {1} bears successful({2})", _winningBears, _bears,
                    (_bears > 0) ? (double)_winningBears / _bears : 0));
                Print(string.Format("{0} of {1} all successful({2})", _winningBears + _winningBulls, _bears + _bulls,
                    (_bears + _bulls > 0) ? (double)(_winningBears + _winningBulls) / (_bears + _bulls) : 0));


                //if (DateTime.Parse(Time.ToString()).Minute > 21)
                //  return;


                var barTime = DateTime.Parse(Time.ToString());


                var isBull = IsBull();


                var isBear = IsBear();


                if (isBull || isBear)
                {
                    var expiryTime = barTime.AddHours(1);

                    var
                        order = new ActiveOrder
                        {
                            Id = Guid.NewGuid(),
                            Time = barTime,
                            ExpiryHour = expiryTime.Hour,
                            ExpiryDay = expiryTime.Day,
                            EnteredAt = Close[0],
                            StrikeWidth = _strikeWidth
                        };


                    if (isBull)
                    {
                        order.IsLong = true;
                        order.ExitAt = Close[0] + (Math.Abs(_strikeWidth));
                        order.SettleAT = Close[0] + (Math.Abs(_strikeWidth * .25));
                        _activerOrders.Add(order.Id, order);
                        _bulls++;

                        SendNotification(order);
                    }


                    if (isBear)
                    {
                        order.IsLong = false;
                        order.ExitAt = Close[0] - (Math.Abs(_strikeWidth));
                        order.SettleAT = Close[0] - (Math.Abs(_strikeWidth * .25));

                        _activerOrders.Add(order.Id, order);
                        _bears++;
                        SendNotification(order);
                    }
                }
            }
            catch (Exception e)
            {
                Print("error found:" + e.Message + " " + e.Source + " " + e.StackTrace);
                //Log("error found:" + e.Message + " " + e.Source + " " + e.StackTrace, LogLevel.Error);
            }
        }

        private void RecordSmaSlope()
        {
            var slope = CalculateCurrentSmaSlope();

            if (_smaSlopeHistory.ContainsKey(this.CurrentBar))
                _smaSlopeHistory[this.CurrentBar] = slope;
            else
            {
                _smaSlopeHistory.Add(this.CurrentBar, slope);
            }
        }

        private double CalculateCurrentSmaSlope()
        {
            return this.Slope(this.SMA(6), 2, 0);
        }

        private bool IsBear()
        {
            

            if (!HasEnoughVoltility())
                return false;

            if (!_smaSlopeHistory.ContainsKey(this.CurrentBar - 1))
                return false;


            var currentSlope = CalculateCurrentSmaSlope();
            var priorSlope = _smaSlopeHistory[this.CurrentBar - 1];

            return (currentSlope < 0 && priorSlope > 0);



        }

       


        private bool IsBull()
        {

            if (!HasEnoughVoltility())
               return false;



       
            
            

            return false;



        }

 


 

        



        private bool HasEnoughVoltility()
        {
           return this.Bollinger(2,12).Upper[0] - this.Bollinger(2,12).Lower[0] > _strikeWidth * 2;

        }

        private void SendNotification(ActiveOrder order)
        {
            var instrumentName = Instrument.ToString().Replace("Default", "").Replace(" ", "").Replace("$", "");

            var mailSubject = string.Format("KC-SIGNAL-{0}:  {1} @  {2}", instrumentName,
                (order.IsLong) ? "BULL" : "BEAR",
                Close[0]);
            var mailContentTemplate = @"A {0} signal was observed in '{1}' at {2} at a closing price of {3}.
Exit at {4}
Strike Width: {5}";
            var mailContent = string.Format(mailContentTemplate, (order.IsLong) ? "BULL" : "BEAR", Instrument, Time,
                Close[0], order.ExitAt, order.StrikeWidth);

            if (Historical)
                return;
            SendMail("hoskinsken@gmail.com", "hoskinsken@gmail.com", mailSubject, mailContent);
        }

        private void HandleCurrentOrders()
        {
            if (!_activerOrders.Any())
                return;


            var currentNow = DateTime.Parse(Time.ToString());

            var successfulBulls = _activerOrders.Values.Where(z => z.IsLong && High[0] >= z.ExitAt).ToList();
            foreach (var success in successfulBulls)
            {
                _activerOrders.Remove(success.Id);
                _winningBulls++;
            }


            var successfulBears = _activerOrders.Values.Where(z => !z.IsLong && Low[0] <= z.ExitAt).ToList();
            foreach (var success in successfulBears)
            {
                _activerOrders.Remove(success.Id);
                _winningBears++;
            }


            if (currentNow.Minute == 00)
            {
                var closingOrders =
                    _activerOrders.Values.Where(
                        z => z.ExpiryDay == currentNow.Day && z.ExpiryHour == currentNow.Hour)
                        .ToList();

                foreach (var candidate in closingOrders)
                {
                    if (candidate.IsLong && candidate.SettleAT < Open[0])
                    {
                        _winningBulls++;
                    }
                    if (!candidate.IsLong && candidate.SettleAT > Open[0])
                    {
                        _winningBears++;
                    }
                    _activerOrders.Remove(candidate.Id);
                }
            }

        }


        private class ActiveOrder
        {
            public double StrikeWidth { get; set; }
            public Guid Id { get; set; }
            public DateTime Time { get; set; }
            public int ExpiryHour { get; set; }
            public int ExpiryDay { get; set; }
            public bool IsLong { get; set; }
            public double EnteredAt { get; set; }
            public double ExitAt { get; set; }
            public double SettleAT { get; set; }
        }
    }
}