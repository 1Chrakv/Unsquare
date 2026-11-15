using System;
using System.Collections.Generic;

namespace Unsquare.Core.Scheduling
{
    /// <summary>Where a group member's swap stands.</summary>
    public enum SwapMemberState
    {
        /// <summary>Still debouncing, baking or waiting for its prefab.</summary>
        Waiting,

        /// <summary>Its swap could run now.</summary>
        Ready,

        /// <summary>Swapped, failed or no longer requested: the group no longer waits for it.</summary>
        Done,
    }

    /// <summary>
    /// Holds the swaps of a group until every member is ready, so they all land in the same frame (e.g. a converted
    /// row Placemaker must not re-plan in between). A group opens anyway at its deadline.
    /// </summary>
    public sealed class SwapGroups<T>
    {
        private sealed class Group
        {
            public readonly HashSet<T> Members;
            public readonly double Deadline;
            public bool Open;

            public Group(HashSet<T> members, double deadline)
            {
                Members = members;
                Deadline = deadline;
            }
        }

        private readonly List<Group> _groups = new();
        private readonly Dictionary<T, Group> _byMember = new();
        private readonly List<T> _scratch = new();

        public int Count => _groups.Count;

        /// <summary>Starts a group; a member already in another group moves to this one.</summary>
        public void Add(IEnumerable<T> members, double deadline)
        {
            if (members == null)
                throw new ArgumentNullException(nameof(members));
            var group = new Group(new HashSet<T>(members), deadline);
            foreach (var member in group.Members)
            {
                if (_byMember.TryGetValue(member, out var old))
                    old.Members.Remove(member);
                _byMember[member] = group;
            }
            _groups.Add(group);
        }

        /// <summary>
        /// Drops finished members and empty groups, then opens groups whose members are all ready or whose deadline
        /// passed. Returns how many groups opened at their deadline.
        /// </summary>
        public int Update(Func<T, SwapMemberState> state, double now)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            int expired = 0;
            for (int g = _groups.Count - 1; g >= 0; g--)
            {
                var group = _groups[g];
                _scratch.Clear();
                bool allReady = true;
                foreach (var member in group.Members)
                {
                    var memberState = state(member);
                    if (memberState == SwapMemberState.Done)
                        _scratch.Add(member);
                    else if (memberState == SwapMemberState.Waiting)
                        allReady = false;
                }
                foreach (var member in _scratch)
                {
                    group.Members.Remove(member);
                    _byMember.Remove(member);
                }
                if (group.Members.Count == 0)
                {
                    _groups.RemoveAt(g);
                    continue;
                }
                if (group.Open)
                    continue;
                if (allReady)
                    group.Open = true;
                else if (now >= group.Deadline)
                {
                    group.Open = true;
                    expired++;
                }
            }
            return expired;
        }

        /// <summary>True while the member's group still waits for others; its swap must not run yet.</summary>
        public bool IsHeld(T member) => _byMember.TryGetValue(member, out var group) && !group.Open;

        public void Clear()
        {
            _groups.Clear();
            _byMember.Clear();
        }
    }
}
