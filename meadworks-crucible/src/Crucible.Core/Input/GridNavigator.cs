// Settle rule adapted for Crucible from OpenKeep PackPanel/PackPanel/src/Ring/KeyRingGamepad.cs
// (https://github.com/geraldjglasgow/ValheimMods, revision 31d8ee35ac4ccfd14c83aa930e4b602ac4dfa230).
// OpenKeep - a Valheim mod. Copyright (C) 2026 Gerald Glasgow.
// Modifications Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public
// License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; see licenses/GPL-3.0.txt and docs/third-party-notices.md.
// Change: generalised from the player inventory ring to any grid with caller-supplied visibility and stand-in cells.
using System;
namespace ExtraPotions.Meadworks.Crucible {
    public readonly struct GridCell:IEquatable<GridCell> {
        public int X { get; }
        public int Y { get; }
        public GridCell(int x,int y){X=x;Y=y;}
        public bool Equals(GridCell other)=>X==other.X&&Y==other.Y;
        public override bool Equals(object? obj)=>obj is GridCell other&&Equals(other);
        public override int GetHashCode()=>(X*397)^Y;
        public static bool operator==(GridCell left,GridCell right)=>left.Equals(right);
        public static bool operator!=(GridCell left,GridCell right)=>!left.Equals(right);
        public override string ToString()=>"("+X+","+Y+")";
    }

    /// <summary>
    /// Gamepad movement over a grid whose cells may be hidden. A move that lands on a hidden cell carries on the same
    /// way to the next cell that shows, or stays where it was (adopted KeyRingGamepad rule). A stand-in maps a cell to
    /// the cell that represents it (a closed pop-up's button).
    /// </summary>
    public sealed class GridNavigator {
        readonly Func<GridCell,bool> shows;
        readonly Func<GridCell,GridCell> standIn;
        public int Width { get; }
        public int Height { get; }
        public bool Wrap { get; }
        public GridNavigator(int width,int height,Func<GridCell,bool> shows,Func<GridCell,GridCell>? standIn=null,bool wrap=false){
            if(width<=0)throw new ArgumentOutOfRangeException(nameof(width));if(height<=0)throw new ArgumentOutOfRangeException(nameof(height));
            Width=width;Height=height;Wrap=wrap;this.shows=shows??throw new ArgumentNullException(nameof(shows));this.standIn=standIn??(c=>c);
        }
        public bool Contains(GridCell cell)=>cell.X>=0&&cell.Y>=0&&cell.X<Width&&cell.Y<Height;

        /// <summary>Where a native move from <paramref name="before"/> to <paramref name="landed"/> should end.</summary>
        public GridCell Settle(GridCell before,GridCell landed)=>landed==before?before:Settle(before,landed,Math.Sign(landed.X-before.X),Math.Sign(landed.Y-before.Y));
        /// <summary>One step in <paramref name="direction"/> (signs only), skipping hidden cells.</summary>
        public GridCell Move(GridCell from,GridCell direction){
            int dx=Math.Sign(direction.X),dy=Math.Sign(direction.Y);
            if(dx==0&&dy==0)return from;
            var landed=new GridCell(from.X+dx,from.Y+dy);
            if(!Contains(landed)){if(!Wrap)return from;landed=Wrapped(landed);}
            return Settle(from,landed,dx,dy);
        }
        GridCell Settle(GridCell before,GridCell landed,int dx,int dy){
            var position=landed;
            for(int step=0;step<Width*Height;step++){
                if(!Contains(position)){if(!Wrap)break;position=Wrapped(position);}
                if(position==before&&step>0)break;
                var target=standIn(position);
                if(Contains(target)&&shows(target)&&target!=before)return target;
                position=new GridCell(position.X+dx,position.Y+dy);
            }
            return before;
        }
        /// <summary>First visible cell in reading order, or null when none shows.</summary>
        public GridCell? First(){
            for(int y=0;y<Height;y++)for(int x=0;x<Width;x++){var cell=standIn(new GridCell(x,y));if(Contains(cell)&&shows(cell))return cell;}
            return null;
        }
        GridCell Wrapped(GridCell cell)=>new GridCell(((cell.X%Width)+Width)%Width,((cell.Y%Height)+Height)%Height);
    }
}
