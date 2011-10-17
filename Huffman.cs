using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OggVorbis
{
    public abstract class HuffNode
    {
        public HuffInternalNode Parent;
        public HuffNode One;
        public HuffNode Zero;
        public bool Internal;
        public int Code = -1;
        
        public int Depth
        {
            get
            {
                return Parent != null ? (Parent.Depth + 1) : 0;
            }
        }

        public void Dump()
        {
            Dump(0);
        }

        public abstract bool IsComplete { get; }
        public abstract int LeafCount { get; }

        public abstract void Dump(int currentDepth);
        public abstract HuffLeafNode Insert(int code, int desiredDepth);

        public abstract void Visit(Action<HuffLeafNode> action);
    }

    public class HuffInternalNode : HuffNode
    {
        public override bool IsComplete
        {
            get { return Zero != null && One != null; }
        }

        public HuffInternalNode()
        {
            Internal = true;
        }

        public override int LeafCount
        {
            get
            {
                int result = 0;
                if (Zero != null)
                    result += Zero.LeafCount;
                if(One != null)
                    result += One.LeafCount;
                return result;
            }
        }

        public override HuffLeafNode Insert(int code, int desiredDepth)
        {
            if (desiredDepth > 1)
            {
                if (Zero == null)
                    Zero = new HuffInternalNode() { Parent = this };

                HuffLeafNode h = Zero.Insert(code, desiredDepth-1);
                if (h != null)
                    return h;

                if (One == null)
                    One = new HuffInternalNode() { Parent = this };

                h = One.Insert(code, desiredDepth-1);
                if (h != null)
                    return h;

                return null;
            }
            else if (desiredDepth == 1)
            {
                if (Zero != null && One != null)
                    return null;

                HuffLeafNode leaf = new HuffLeafNode() { Parent=this, Code = code };

                if (Zero == null)
                    Zero = leaf;
                else
                    One = leaf;

                return leaf;
            }
            else
                throw new ArgumentOutOfRangeException("desiredDepth");
        }

        public override void Dump(int currentDepth)
        {
            if (Zero != null)
                Zero.Dump(currentDepth + 1);
            if (One != null)
                One.Dump(currentDepth + 1);
        }

        public override void Visit(Action<HuffLeafNode> action)
        {
            if (Zero != null)
                Zero.Visit(action);
            if (One != null)
                One.Visit(action);
        }
    }

    public class HuffLeafNode : HuffNode
    {

        public override bool IsComplete
        {
            get { return true; }
        }

        public override int LeafCount
        {
            get { return 1; }
        }

        public HuffLeafNode()
        {
            Internal = false;
        }

        public override HuffLeafNode Insert(int code, int desiredDepth)
        {
            return null;
        }

        public override void Dump(int currentDepth)
        {
            for (int i = 0; i < currentDepth; ++i)
                Console.Write(" ");
            Console.WriteLine("{0}", Code);
        }

        public string Codeword()
        {
            string result = "";
            HuffInternalNode cur = Parent;
            HuffNode cur2 = this;
            while (cur != null)
            {
                if (cur2 == cur.One)
                    result = "1" + result;
                else
                    result = "0" + result;
                cur2 = cur;
                cur = cur.Parent;
            }
            return result;
        }

        public override void Visit(Action<HuffLeafNode> action)
        {
            action(this);
        }
    }
}
