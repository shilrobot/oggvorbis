import matplotlib.pyplot as plt

def save_xy_chart(line,fn):
	print 'Writing: '+fn
	parts = line.split(',')[:-1]
	parts = [int(x) for x in parts]
	points = []
	for n in range(len(parts)/2):
		#print parts[n*2], parts[n*2+1]
		points.append((parts[n*2], parts[n*2 + 1]))
	points.sort()
	xs = []
	ys = []
	for (x,y) in points:
		xs.append(x)
		ys.append(y)
	plt.plot(xs,ys)
	plt.savefig(fn, dpi=100)
	plt.clf()
	
def save_y_chart(line,fn):
	print 'Writing: '+fn
	parts = line.split(',')[:-1]
	parts = [float(x) for x in parts]
	plt.plot(parts)
	plt.savefig(fn, dpi=100)
	plt.clf()

M=0
for line in open('out.txt'):
	line = line.strip()
	#if line.startswith('>>>'):
	#	save_xy_chart(line[4:], 'out/out-%08d-a.png'%M)
	#elif line.startswith('$$$'):
	#	save_xy_chart(line[4:], 'out/out-%08d-b.png'%M)
	#elif line.startswith('###'):
	#	save_y_chart(line[4:], 'out/out-%08d-c.png'%M)
	#if line.startswith('FLOOR: '):
	#	save_y_chart(line[len('FLOOR: '):], 'out/out-%08d-a.png'%M)
		#M+=1
	#if line.startswith('RESIDUE: '):
	#	save_y_chart(line[len('RESIDUE: '):], 'out/out-%08d-b.png'%M)
		#M+=1
	#if line.startswith('MDCT_IN: '):
	#	save_y_chart(line[len('MDCT_IN: '):], 'out/out-%08d-c.png'%M)
		#M+=1
	if line.startswith('MDCT_OUT: '):
		save_y_chart(line[len('MDCT_OUT: '):], 'out/out-%08d-d.png'%M)
		M+=1
	#if line.startswith('DP: '):
	#	save_y_chart(line[4:], 'out/out-%08d.png'%M)
	#	M+=1