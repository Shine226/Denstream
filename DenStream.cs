using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class DenStream : MonoBehaviour {

	public List<MicroCluster> p_micro_cluster;
	public List<MicroCluster> o_micro_cluster;
	public long timestamp = 0;
	float epsilon;
	float beta;
	float mu;
	long currentTimestamp;
	float lambda;
	long tp;
	bool initialized;
	int minPoints;
	List<DenPoint> initBuffer;
	int initN;

	// Use this for initialization
	void Start () {
		setLearningPara ();
	}

	public void startDenStream(float[] point){
		DenPoint p = new DenPoint (point[0], point[1]);

		DenStreamAlgorithm(p);
	}

	public void setLearningPara(){
		epsilon = 3f;
		beta = 0.2f;
		mu = 1f;
		lambda = 0.25f;
		initN = 10;
		minPoints = 2;

		initialized = false;
		tp = Convert.ToInt64(Math.Round (1 / lambda * Math.Log ((beta * mu) / (beta * mu - 1))));
		p_micro_cluster = new List<MicroCluster> ();
		o_micro_cluster = new List<MicroCluster> ();
		initBuffer = new List<DenPoint> ();
	}

	public void initialDBScan(){
		for (int i = 0; i < initBuffer.Count; i++) {
			DenPoint point = initBuffer [i];
			if (!point.covered) {
				point.covered = true;
				List<int> neighbourhood = getNeighbourhoodIDs (point, initBuffer);
				if (neighbourhood.Count >= minPoints) {
					MicroCluster mc = new MicroCluster (point.toArray(), 2, timestamp, lambda, currentTimestamp);
					expandCluster (mc, initBuffer, neighbourhood);
					p_micro_cluster.Add (mc);
				} else {
					point.covered = false;
				}
			}
		
		}
	}

	public List<int> getNeighbourhoodIDs(DenPoint point, List<DenPoint> points){
		List<int> neighbourIDs = new List<int> ();
		for (int i = 0; i < points.Count; i++) {
			DenPoint testPoint = points [i];
			if (!testPoint.covered) {
				float dist = Distance (testPoint, point);
				if (dist < epsilon) {
					neighbourIDs.Add (i);
				}
			}		
		}
		return neighbourIDs;
	}

	public void expandCluster(MicroCluster mc, List<DenPoint> points, List<int> neighbourhood) {
		for (int i = 0; i < neighbourhood.Count; i++) {
			DenPoint testPoint = points [neighbourhood[i]];
			if (!testPoint.covered) {
				testPoint.covered = true;
				mc.insert (testPoint, timestamp);
				List<int> neighbourhood2 = getNeighbourhoodIDs (testPoint, initBuffer);
				if (neighbourhood.Count >= minPoints) {
					expandCluster (mc, points, neighbourhood2);
				}
			}
		}
	}

	public void DenStreamAlgorithm(DenPoint point){
		timestamp++;
		point.setTimestamp (timestamp);
		if (!initialized) {
			initBuffer.Add (point);
			if (initBuffer.Count >= initN){
				initialDBScan ();
				initialized = true;
			}

		} else {
			Merging (point, timestamp);
		}

		if (timestamp%tp ==0) {
			List<MicroCluster> removalList = new List<MicroCluster>();

			for (int i=0; i<p_micro_cluster.Count;i++){
				if (p_micro_cluster [i].getWeight () < beta * mu) {
					removalList.Add (p_micro_cluster [i]);
				}
			}

			for (int i = 0; i < removalList.Count; i++) {
				p_micro_cluster.Remove (removalList [i]);
			}

			for (int i=0; i<o_micro_cluster.Count;i++){
				long t0 = o_micro_cluster [i].getCreationTime();
				float xsi1 = Mathf.Pow (2, (-lambda * (timestamp - t0 + tp))) - 1;
				float xsi2 = Mathf.Pow (2, (-lambda * tp)) - 1;
				float xsi = xsi1 / xsi2;

				if (o_micro_cluster [i].getWeight () < xsi) {
					removalList.Add (o_micro_cluster [i]);
				}
			}

			for (int i = 0; i < removalList.Count; i++) {
				o_micro_cluster.Remove (removalList [i]);
			}

		}
	}

	public void Merging(DenPoint point, long timestamp) 
	{
		currentTimestamp = timestamp;
		Boolean merged = false;
		if (p_micro_cluster.Count !=  0){
			MicroCluster x = nearestCluster(point, p_micro_cluster);
			MicroCluster xCopy = x.copy ();

			xCopy.insert (point, timestamp);

			if (xCopy.getRadius (timestamp) <= epsilon) {
				x.insert (point, timestamp);
				merged = true;
			}
		}

		if (!merged && o_micro_cluster.Count != 0) {
			MicroCluster x = nearestCluster (point, o_micro_cluster);
			MicroCluster xCopy = x.copy ();
			xCopy.insert (point, timestamp);

			if (xCopy.getRadius (timestamp) <= epsilon) {
				x.insert (point, timestamp);
				merged = true;
				if (x.getWeight () > beta * mu) {
					o_micro_cluster.Remove (x);
					p_micro_cluster.Add (x);
				}
			}
		}

		if (!merged) {
			o_micro_cluster.Add (new MicroCluster (point.toArray(), 2, timestamp, lambda, currentTimestamp));
		}
	}

	public MicroCluster nearestCluster (DenPoint point, List<MicroCluster> cl){
		MicroCluster min = null;
		float minDist = 0;
		for (int c = 0; c < cl.Count; c++){
			MicroCluster cluster = cl[c];
			if (min ==null){
				min = cluster;
			}
			float dist = Distance(point, cluster.getCenter());
			dist -= cluster.getRadius (timestamp);

			if (c == 0) {
				minDist = dist;
			} else {
				if (dist < minDist) {
					minDist = dist;
					min = cluster;
			
				}
			}
		}

		return min;

	}
		
	public float Distance(DenPoint a, DenPoint b){
		float sumSquaredDiffs = 0;
		float tempX = 0;
		float tempY = 0;

		tempX = a.X - b.X;
		tempY = a.Y - b.Y;
		sumSquaredDiffs = tempX * tempX + tempY * tempY;

		return (float)Math.Sqrt (sumSquaredDiffs);
	}

	public class DenPoint
	{
		public float X;
		public float Y;
		public long timestamp;
		public bool covered; 

		public DenPoint( float x, float y, long t )
		{
			X = x;
			Y = y;
			timestamp = t;
		}

		public DenPoint(){
		}

		public DenPoint( float x, float y)
		{
			X = x;
			Y = y;
		}

		public float[] toArray(){
			float[] p = new float[2];
			p [0] = this.X;
			p [1] = this.Y;
			return p;
		}

		public void setTimestamp(long timestamp){
			this.timestamp = timestamp;
		}
	}

	public class MicroCluster
	{
		public float[] LS;
		public float[] SS;
		public int N;
		public long lastEditT = -1;
		public float lambda;
		public long currentTimestamp;
		private long creationTimestamp = -1;
		float weight;

		public MicroCluster(float[] center, int dimensions, long creationTimestamp, float lambda, long currentTimestamp){
			this.creationTimestamp = creationTimestamp;
			this.lastEditT = creationTimestamp;
			this.lambda = lambda;
			this.currentTimestamp = currentTimestamp;
			this.N = 1;
			this.LS = center;
			this.SS = new float[dimensions];
			for (int i=0; i<SS.Length; i++){
				SS[i]=center[i] * center[i];

			}
		}

		public void insert(DenPoint point, long timstamp) {
			N++;
			this.lastEditT = timstamp;

			// weight = N + 1;

			LS [0] += point.X;
			LS [1] += point.Y;
			SS [0] += point.X * point.X;
			SS [1] += point.Y * point.Y;
		}

		public DenPoint getCenter(){
		
			return getCenter (currentTimestamp);
		}

		public DenPoint getCenter(long timestamp){
			float dt = timestamp - lastEditT;
			float w = getWeight (timestamp);

			DenPoint result = new DenPoint();
			float[] resultF = new float[LS.Length];
			for (int i = 0; i < LS.Length; i++) {
				resultF [i] = LS [i];
				float fn = Mathf.Pow (2, -lambda * dt);

				//resultF [i] *= Mathf.Pow (2, -lambda * dt);
				resultF [i] *= fn;
				resultF [i] /= w;
			}

			result.X = resultF [0];
			result.Y = resultF [1];

			return result;
		}


		public float getWeight(){
			return getWeight (currentTimestamp);
		}

		public float getWeight(long timestamp) {
			float dt = timestamp - lastEditT;
			return (N * Mathf.Pow (2, -lambda * dt));
		}

		public float getRadius(){
			return getRadius (currentTimestamp);
		}

		public float getRadius(long timestamp){
			long dt = timestamp - lastEditT;
			float[] cf1 = calcCF1 (dt);
			float[] cf2 = calcCF2 (dt);

			float w = getWeight (timestamp);
			float max = 0;

			for (int i = 0; i < SS.Length; i++) {
				float x1 = cf2 [i] / w;
				float temp = cf1 [i] / w;
				float x2 = temp * temp;
				// float x3 = Mathf.Pow (cf1 [i] / w, 2);
				float diffSqrt = Mathf.Sqrt(x1-x2);
				if (diffSqrt > max) {
					max = diffSqrt;
				}
			}

			return max;
		}

		public float[] calcCF1(long dt) {
			float[] cf1 = new float[LS.Length];

			for (int i = 0; i < LS.Length; i++) {
				cf1 [i] = Mathf.Pow (2, -lambda * dt) * LS [i];
			}
			return cf1;
		}

		public float[] calcCF2(long dt) {
			float[] cf2 = new float[SS.Length];
			for (int i = 0; i < SS.Length; i++) {
				cf2 [i] = Mathf.Pow (2, -lambda * dt) * SS [i];
			}
			return cf2;
		}

		public long getCreationTime() {
			return creationTimestamp;
		}

		public MicroCluster copy(){
			MicroCluster copy = new MicroCluster ((float[])this.LS.Clone(), this.LS.Length, this.getCreationTime(), this.lambda, this.currentTimestamp);
			copy.N = this.N;
			copy.SS = (float[])this.SS.Clone();
			copy.LS = (float[])this.LS.Clone();
			copy.lastEditT = this.lastEditT;
			return copy;
		}
	}
		
}
