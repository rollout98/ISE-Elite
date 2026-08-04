using System;
using System.Collections.Generic;

namespace ISE.ExecutionQuality
{
    public enum ExecutionQualityState
    {
        Ideal,
        Acceptable,
        Early,
        Late,
        Chasing,
        PoorLiquidity,
        Blocked
    }

    public enum ExecutionPosture
    {
        FullSize,
        ReducedSize,
        Wait,
        StandAside
    }

    public sealed class ExecutionQualityInput
    {
        public ExecutionQualityInput(decimal distanceFromReferenceTicks, decimal pullbackCompletion,
            decimal liquidityScore, decimal spreadScore, decimal momentumExhaustion,
            bool confirmationPresent, bool authoritativeRiskBlock = false)
        {
            DistanceFromReferenceTicks = distanceFromReferenceTicks;
            PullbackCompletion = Normalized(pullbackCompletion, nameof(pullbackCompletion));
            LiquidityScore = Normalized(liquidityScore, nameof(liquidityScore));
            SpreadScore = Normalized(spreadScore, nameof(spreadScore));
            MomentumExhaustion = Normalized(momentumExhaustion, nameof(momentumExhaustion));
            ConfirmationPresent = confirmationPresent;
            AuthoritativeRiskBlock = authoritativeRiskBlock;
        }

        public decimal DistanceFromReferenceTicks { get; }
        public decimal PullbackCompletion { get; }
        public decimal LiquidityScore { get; }
        public decimal SpreadScore { get; }
        public decimal MomentumExhaustion { get; }
        public bool ConfirmationPresent { get; }
        public bool AuthoritativeRiskBlock { get; }

        private static decimal Normalized(decimal value, string name)
        {
            if (value < 0 || value > 1) throw new ArgumentOutOfRangeException(name);
            return value;
        }
    }

    public sealed class ExecutionQualityDecision
    {
        public ExecutionQualityDecision(ExecutionQualityState state, ExecutionPosture posture,
            int score, IReadOnlyList<string> reasons)
        {
            State = state;
            Posture = posture;
            Score = score;
            Reasons = reasons;
        }

        public ExecutionQualityState State { get; }
        public ExecutionPosture Posture { get; }
        public int Score { get; }
        public IReadOnlyList<string> Reasons { get; }
    }

    public sealed class ExecutionQualityEngine
    {
        public ExecutionQualityDecision Evaluate(ExecutionQualityInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            var reasons = new List<string>();

            if (input.AuthoritativeRiskBlock)
            {
                reasons.Add("Execution cannot override an authoritative risk block.");
                return new ExecutionQualityDecision(ExecutionQualityState.Blocked,
                    ExecutionPosture.StandAside, 0, reasons);
            }

            if (input.LiquidityScore < 0.35m || input.SpreadScore < 0.35m)
            {
                reasons.Add("Liquidity or spread conditions are unsuitable for entry.");
                return new ExecutionQualityDecision(ExecutionQualityState.PoorLiquidity,
                    ExecutionPosture.StandAside, 25, reasons);
            }

            if (input.DistanceFromReferenceTicks > 18 || input.MomentumExhaustion >= 0.75m)
            {
                reasons.Add("Price is extended from the decision reference and entry would chase momentum.");
                return new ExecutionQualityDecision(ExecutionQualityState.Chasing,
                    ExecutionPosture.StandAside, 30, reasons);
            }

            if (!input.ConfirmationPresent || input.PullbackCompletion < 0.45m)
            {
                reasons.Add("The setup has not completed confirmation or its pullback.");
                return new ExecutionQualityDecision(ExecutionQualityState.Early,
                    ExecutionPosture.Wait, 50, reasons);
            }

            if (input.DistanceFromReferenceTicks > 10 || input.PullbackCompletion > 0.92m)
            {
                reasons.Add("The preferred entry location has substantially passed.");
                return new ExecutionQualityDecision(ExecutionQualityState.Late,
                    ExecutionPosture.ReducedSize, 60, reasons);
            }

            decimal composite = input.PullbackCompletion * 0.35m
                + input.LiquidityScore * 0.30m
                + input.SpreadScore * 0.20m
                + (1m - input.MomentumExhaustion) * 0.15m;

            int score = (int)Math.Round(composite * 100m, MidpointRounding.AwayFromZero);

            if (score >= 82 && input.DistanceFromReferenceTicks <= 6)
            {
                reasons.Add("Confirmation, location, liquidity, and spread conditions align.");
                return new ExecutionQualityDecision(ExecutionQualityState.Ideal,
                    ExecutionPosture.FullSize, score, reasons);
            }

            reasons.Add("The entry is valid but does not meet ideal execution-quality thresholds.");
            return new ExecutionQualityDecision(ExecutionQualityState.Acceptable,
                ExecutionPosture.ReducedSize, score, reasons);
        }
    }
}
