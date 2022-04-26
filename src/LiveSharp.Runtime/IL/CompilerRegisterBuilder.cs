using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using LiveSharp.Runtime.Infrastructure;
using Range = LiveSharp.Runtime.Infrastructure.Range;

namespace LiveSharp.Runtime.IL
{
    class CompilerRegisterBuilder
    {
        public List<ParameterExpression> Registers { get; } = new List<ParameterExpression>();
        
        private readonly List<StackSlotExpression> _stackSlots;
        private readonly ConcurrentDictionary<int, List<Range>> _stackSlotUsageRanges = new ConcurrentDictionary<int, List<Range>>();
        private readonly ConcurrentDictionary<int, ParameterExpression> _stackSlotParameterSubstitutes = new ConcurrentDictionary<int, ParameterExpression>();
        private readonly ConcurrentDictionary<int, List<int>> _stackSlotUsageLocations;
        private readonly ConcurrentDictionary<int, List<int>> _mergedSlots;

        public CompilerRegisterBuilder(List<StackSlotExpression> stackSlots,
            ConcurrentDictionary<int, List<int>> stackSlotUsageLocations,
            ConcurrentDictionary<int, List<int>> mergedSlots)
        {
            _stackSlots = stackSlots;
            _stackSlotUsageLocations = stackSlotUsageLocations;
            _mergedSlots = mergedSlots;
        }

        public void CreateStackSlotParameterSubstitutes()
        {
            var processedSlots = new HashSet<int>();
            var allMergedSlots = new HashSet<int>(_mergedSlots.SelectMany(kvp => kvp.Value));
            
            foreach (var leftSlot in _stackSlots) {
                // Ignore slots that are already grouped
                if (processedSlots.Contains(leftSlot.InstructionIndex))
                    continue;
                // Ignore slots that to be merged with others
                if (allMergedSlots.Contains(leftSlot.InstructionIndex)) {
                    // Make sure we create substitute when leftSlot is _mergedSlots key
                    // Example `src -> leftSlot -> merge` case
                    if (_mergedSlots.ContainsKey(leftSlot.InstructionIndex))
                        CreateParameterSubstitute(leftSlot, new List<int>());
                    continue;
                }

                // Make sure we don't process same slot twice
                processedSlots.Add(leftSlot.InstructionIndex);
                
                var leftRanges = _stackSlotUsageRanges[leftSlot.InstructionIndex];
                var combined = new List<int>();
                
                foreach (var rightSlot in _stackSlots) {
                    // Ignore slots that are already grouped
                    if (processedSlots.Contains(rightSlot.InstructionIndex))
                        continue;
                    // Ignore slots that to be merged with others (ignore merge source too)
                    if (allMergedSlots.Contains(rightSlot.InstructionIndex) || _mergedSlots.ContainsKey(rightSlot.InstructionIndex))
                        continue;
                    
                    if (CanCombine(leftSlot, rightSlot, leftRanges, out var rightRanges)) {
                        combined.Add(rightSlot.InstructionIndex);
                        processedSlots.Add(rightSlot.InstructionIndex);
                        leftRanges.AddRange(rightRanges);
                    }
                }
                
                CreateParameterSubstitute(leftSlot, combined);
            }
        }

        private void CreateParameterSubstitute(StackSlotExpression slot, List<int> combined)
        {
            var slotType = slot.Type;
            
            if (!_stackSlotParameterSubstitutes.TryGetValue(slot.InstructionIndex, out var parameterSubstitute) || parameterSubstitute.Type != slotType) {
                parameterSubstitute = Expression.Parameter(slotType, $"r{Registers.Count}_{slotType.Name}");
                _stackSlotParameterSubstitutes[slot.InstructionIndex] = parameterSubstitute;
                Registers.Add(parameterSubstitute);
            }
            
            foreach (var slotIndex in combined)
                _stackSlotParameterSubstitutes[slotIndex] = parameterSubstitute;
            
            if (_mergedSlots.TryGetValue(slot.InstructionIndex, out var merged)) {
                foreach (var mergedIndex in merged) 
                    _stackSlotParameterSubstitutes[mergedIndex] = parameterSubstitute;
            }
        }

        private bool CanCombine(StackSlotExpression leftSlot, StackSlotExpression rightSlot, List<Range> leftRanges, out List<Range> rightRanges)
        {
            rightRanges = _stackSlotUsageRanges[rightSlot.InstructionIndex];
            if (leftSlot == rightSlot)
                return false;
            
            if (rightSlot.Type != leftSlot.Type)
                return false;

            return !anyIntersections(rightRanges);

            bool anyIntersections(IReadOnlyList<Range> otherRanges)
            {
                foreach (var range in leftRanges)
                foreach (var otherSlotRange in otherRanges)
                    if (range.Intersects(otherSlotRange))
                        return true;

                return false;
            }
        }

        public void CreateStackSlotRanges()
        {
            foreach (var stackSlot in _stackSlots) {
                var locations = _stackSlotUsageLocations.ContainsKey(stackSlot.InstructionIndex)
                    ? _stackSlotUsageLocations[stackSlot.InstructionIndex]
                    : new List<int>();

                AppendMergeSourceLocations(stackSlot.InstructionIndex, locations);
                
                locations.Sort();

                if (locations.Count == 0)
                    throw new InvalidOperationException($"StackSlot {stackSlot} with 0 usages");

                var ranges = CreateRangesFromLocations(locations);

                _stackSlotUsageRanges[stackSlot.InstructionIndex] = ranges;
            }
        }

        private void AppendMergeSourceLocations(int slotIndex, List<int> locations)
        {
            foreach (var mergedSlotKvp in _mergedSlots) {
                var mergeSource = mergedSlotKvp.Key;
                var mergedSlots = mergedSlotKvp.Value;

                if (mergedSlots.Contains(slotIndex)) {
                    if (_stackSlotUsageLocations.TryGetValue(mergeSource, out var sourceLocations))
                        locations.AddRange(sourceLocations);
                }
            }
        }

        private static List<Range> CreateRangesFromLocations(IReadOnlyList<int> locations)
        {
            var ranges = new List<Range>();
            var start = locations[0];
            var end = start;

            for (int i = 1; i < locations.Count; i++) {
                if (locations[i] - 1 == end) {
                    end++;
                }
                else {
                    ranges.Add(new Range(start, end));
                    start = locations[i];
                    end = start;
                }
            }
            
            ranges.Add(new Range(start, end));

            return ranges;
        }

        public ConcurrentDictionary<int, ParameterExpression> BuildRegistersFromStackSlots()
        {
            CreateStackSlotRanges();
            CreateStackSlotParameterSubstitutes();

            return _stackSlotParameterSubstitutes;
        }
    }
}