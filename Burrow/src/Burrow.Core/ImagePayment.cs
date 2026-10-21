// Burrow - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Burrow {
    /// <summary>One item of a saved inventory image: its slot, its saved bytes with the stack set to one, and its stack.</summary>
    public readonly struct ImageItem {
        public readonly int Slot;public readonly byte[] Identity;public readonly int Count;
        public ImageItem(int slot,byte[] identity,int count){Slot=slot;Identity=identity??throw new ArgumentNullException(nameof(identity));Count=count;}
    }
    public enum ImageStep {SetCount,Remove,Replace,Add}
    /// <summary>
    /// A position whose item differs between the before and after images. <see cref="Key"/> is the slot and, for the rare
    /// inventory with two items in one slot, which of them in list order. <see cref="AfterIndex"/> is -1 for a removal.
    /// </summary>
    public readonly struct ImageChange {
        public readonly ImageStep Step;public readonly long Key;public readonly int Count,AfterIndex;
        public ImageChange(ImageStep step,long key,int count,int afterIndex){Step=step;Key=key;Count=count;AfterIndex=afterIndex;}
    }
    /// <summary>
    /// Applies a payment image to the live item list by changing only the positions that differ between its before and after
    /// images. Every other live item keeps its instance, list position, durability and custom data: an equipped tool that
    /// another mod changes between payments stays the item the player holds.
    /// </summary>
    public static class ImagePayment {
        public static IReadOnlyList<ImageChange> Changes(IReadOnlyList<ImageItem> before,IReadOnlyList<ImageItem> after){
            var old=ByKey(before,e=>e.Slot);var keys=Keys(after,e=>e.Slot);var changes=new List<ImageChange>();
            for(int i=0;i<after.Count;i++){
                var item=after[i];
                if(!old.TryGetValue(keys[i],out var was))changes.Add(new ImageChange(ImageStep.Add,keys[i],item.Count,i));
                else if(!was.Identity.SequenceEqual(item.Identity))changes.Add(new ImageChange(ImageStep.Replace,keys[i],item.Count,i));
                else if(was.Count!=item.Count)changes.Add(new ImageChange(ImageStep.SetCount,keys[i],item.Count,i));
            }
            var kept=new HashSet<long>(keys);
            foreach(var pair in old)if(!kept.Contains(pair.Key))changes.Add(new ImageChange(ImageStep.Remove,pair.Key,0,-1));
            return changes;
        }
        /// <summary>
        /// Changes <paramref name="live"/> from the before image to the after image. Each changed position must hold the item
        /// the before image recorded there (same identity and stack), or nothing is changed and the call throws. Added and
        /// replacing items come from <paramref name="afterItems"/> (index-aligned with <paramref name="after"/>). Returns the
        /// live items that were removed or replaced.
        /// </summary>
        public static IReadOnlyList<T> Apply<T>(List<T> live,IReadOnlyList<ImageItem> before,IReadOnlyList<ImageItem> after,IReadOnlyList<T> afterItems,
            Func<T,int> slotOf,Func<T,byte[]> identityOf,Func<T,int> countOf,Action<T,int> setCount)where T:class{
            if(afterItems.Count!=after.Count)throw new ArgumentException("After items do not match the after image.");
            var changes=Changes(before,after);var old=ByKey(before,e=>e.Slot);var byKey=ByKey(live,slotOf);
            foreach(var change in changes){
                bool present=byKey.TryGetValue(change.Key,out var current);
                if(change.Step==ImageStep.Add){if(present)throw new InvalidOperationException("Payment slot is no longer empty.");continue;}
                var was=old[change.Key];
                if(!present||countOf(current!)!=was.Count||!identityOf(current!).SequenceEqual(was.Identity))throw new InvalidOperationException("Payment stack changed since the reservation.");
            }
            var removed=new List<T>();
            foreach(var change in changes){
                switch(change.Step){
                    case ImageStep.SetCount:setCount(byKey[change.Key],change.Count);break;
                    case ImageStep.Remove:{var item=byKey[change.Key];live.RemoveAt(IndexOf(live,item));removed.Add(item);break;}
                    case ImageStep.Replace:{var item=byKey[change.Key];live[IndexOf(live,item)]=afterItems[change.AfterIndex];removed.Add(item);break;}
                    case ImageStep.Add:live.Add(afterItems[change.AfterIndex]);break;
                }
            }
            // A saved image lists its items in order; follow the after image's order with the same instances.
            var liveKeys=Keys(live,slotOf);var afterKeys=Keys(after,e=>e.Slot);
            if(live.Count==after.Count&&!liveKeys.SequenceEqual(afterKeys)){
                var at=new Dictionary<long,T>();for(int i=0;i<live.Count;i++)at[liveKeys[i]]=live[i];
                if(afterKeys.All(at.ContainsKey)){var ordered=afterKeys.Select(k=>at[k]).ToArray();live.Clear();live.AddRange(ordered);}
            }
            return removed;
        }
        static int IndexOf<T>(List<T> list,T item)where T:class{for(int i=0;i<list.Count;i++)if(ReferenceEquals(list[i],item))return i;throw new InvalidOperationException("Live item missing.");}
        static long[] Keys<T>(IReadOnlyList<T> items,Func<T,int> slotOf){
            var seen=new Dictionary<int,int>();var keys=new long[items.Count];
            for(int i=0;i<items.Count;i++){int slot=slotOf(items[i]);seen.TryGetValue(slot,out int n);seen[slot]=n+1;keys[i]=((long)slot<<20)+n;}
            return keys;
        }
        static Dictionary<long,T> ByKey<T>(IReadOnlyList<T> items,Func<T,int> slotOf){var keys=Keys(items,slotOf);var result=new Dictionary<long,T>();for(int i=0;i<items.Count;i++)result[keys[i]]=items[i];return result;}
    }
}
