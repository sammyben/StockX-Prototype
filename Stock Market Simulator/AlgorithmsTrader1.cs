﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stock_Market_Simulator {
    public enum Segments { RapidlyRising, Rising, Falling, RapidlyFalling }
    public enum Stance { ShortTermLong, ShortTermShort }
    class AlgorithmsTrader1 : AlgoTrader {
        public List<StockTurn> StockTurns = new List<StockTurn>();
        public Client client = new Client(69, 0.05f);
        List<MarketStance> stances = new List<MarketStance>();
        //Takes a look at last two cycles
        //establishes trend
        //purchases or sell depending on position eg long or short
        public AlgorithmsTrader1(long stocksOwned) {
            StocksOwned = stocksOwned;
        }

        public void RunTurn(List<Trade> Trades) {
            if (Trades.Count == 0) {
                return;
            }
            StockTurns.Add(new StockTurn(Trades));
            CreateNewStance(true);
            if (StockTurns.Count >= 150) {
                StockTurns.RemoveAt(0);
            }
            foreach (MarketStance MS in stances) {
                MS.RunTurn();
            }
            stances.RemoveAll((MarketStance ms) => ms.isCompleted);
        }
        void CreateNewStance(bool ShortTerm) {
            if (StockTurns[StockTurns.Count - 1].AveragePrice < 1f) {
                new MarketStance(Stance.ShortTermLong, 1000, client, this);
            }
            if (ShortTerm) {
                if (StockTurns.Count < 4) {
                    return;
                }
                int TotalLast3Turns = 0;
                for (int i = 1; i <= 3; i++) {
                    TotalLast3Turns += (int)StockTurns[StockTurns.Count - i].Trend + 1;
                }
                float AverageLast3Turns = (float)TotalLast3Turns / 3f;
                if (AverageLast3Turns <= 1.6f) {
                    stances.Add(new MarketStance(Stance.ShortTermLong, MathsHelper.Lerp(10, 100, 1f - (AverageLast3Turns - 1f)), client, this));
                } else if (AverageLast3Turns >= 2.4f) {
                    stances.Add(new MarketStance(Stance.ShortTermShort, MathsHelper.Lerp(10, 100, (AverageLast3Turns - 2.4f) / (4f - 2.4f)), client, this));
                }
            }
        }

        class MarketStance {
            public Stance stance;
            public Client client;
            double SuccessPrice;
            double FailurePrice;
            long StocksOwned;
            long StocksWanted;
            AlgoTrader Owner;
            public bool isCompleted = false;
            bool OfferPlaced = false;
            //TODO: Take market price at which baught at then if price falls below sell and of price increase to a certain point buy
            //Add to a list in main trader which run through on on turn
            public MarketStance(Stance s, long Quanity, Client c, AlgoTrader owner) {
                stance = s;
                Owner = owner;
                switch (stance) {
                    case Stance.ShortTermLong:
                        client = new Client(c.ID, c.LMMPercentage, OnBuyLong, OnSellLong);
                        StocksWanted = Quanity;
                        SuccessPrice = StockTicker.CurrentPrice + 0.10f;
                        FailurePrice = StockTicker.CurrentPrice - 0.05f;
                        ShortTermLong(Quanity);
                        break;
                    case Stance.ShortTermShort:
                        StocksOwned = Quanity;
                        client = new Client(c.ID, c.LMMPercentage, OnBuyShort, OnSellShort);
                        SuccessPrice = StockTicker.CurrentPrice - 0.10f;
                        FailurePrice = StockTicker.CurrentPrice + 0.05f;
                        ShortTermShort(Quanity);
                        break;
                }
            }

            private void ShortTermShort(long Quanity) {
                Pool.AddOffer(new Offers(StockTicker.CurrentPrice, Quanity, client));
            }

            void ShortTermLong(long Quanity) {
                Console.WriteLine("Selling " + Quanity);
                Pool.AddBid(new Bids(StockTicker.CurrentPrice, Quanity, client));
            }

            public void RunTurn() {
                switch (stance) {
                    case Stance.ShortTermLong:
                        if ((StockTicker.CurrentPrice >= SuccessPrice || StockTicker.CurrentPrice < FailurePrice) && !OfferPlaced && StocksWanted == StocksOwned) {
                            Pool.AddOffer(new Offers(StockTicker.CurrentPrice, StocksOwned, client));
                            OfferPlaced = true;
                        }
                        break;
                    case Stance.ShortTermShort:
                        if ((StockTicker.CurrentPrice <= SuccessPrice || StockTicker.CurrentPrice > FailurePrice) && !OfferPlaced && StocksOwned == 0) {
                            Pool.AddBid(new Bids(StockTicker.CurrentPrice, StocksWanted, client));
                            Console.WriteLine("Yeah I done here");
                            OfferPlaced = true;
                        }
                        break;

                }
            }
            public void OnBuyLong(long Quanity) {
                StocksOwned += Quanity;
                //Console.WriteLine("Stocks Baught By Algo! " + Quanity);
            }
            public void OnBuyShort(long Quanity) {
                StocksOwned += Quanity;
                if (StocksWanted == StocksOwned) {
                    isCompleted = true;
                }
            }
            public void OnSellLong(long Quanity) {
                StocksOwned -= Quanity;
                Console.WriteLine("Stocks Sold!");
                if (StocksOwned < 0) {
                    throw new Exception("Negative Stock!");
                }
                if (StocksOwned == 0) {
                    isCompleted = true;
                }
            }
            public void OnSellShort(long Quanity) {
                StocksOwned -= Quanity;
                if (StocksOwned < 0) {
                    throw new Exception("Negative Stock!");
                }
            }
        }
    }

    class StockTurn {
        public double OpeningPrice;
        public double LowPrice;
        public double HighPrice;
        public double ClosePrice;
        public double AveragePrice;
        public Segments Trend;
        public StockTurn(List<Trade> trades) {
            OpeningPrice = trades[0].TradePrice;
            LowPrice = trades.OrderBy((Trade t) => t.TradePrice).ToList()[0].TradePrice;
            HighPrice = trades.OrderByDescending((Trade t) => t.TradePrice).ToList()[0].TradePrice;
            ClosePrice = trades[trades.Count - 1].TradePrice;
            double TotalPrice = 0;
            foreach (Trade t in trades) {
                TotalPrice += t.TradePrice;
            }
            AveragePrice = TotalPrice / (double)trades.Count;
            Trend = AssignSegment();
            //Console.WriteLine(Trend);
        }
        Segments AssignSegment() {
            if (ClosePrice > OpeningPrice) {
                if (ClosePrice > AveragePrice) {
                    return Segments.RapidlyRising;
                } else {
                    return Segments.Rising;
                }
            } else {
                if (ClosePrice > AveragePrice) {
                    return Segments.Falling;
                } else {
                    return Segments.RapidlyFalling;
                }
            }
        }
    }

    public class AlgoTrader {
        public long StocksOwned;
    }
}
