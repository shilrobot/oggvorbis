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
for line in open('goldenfloors.txt'):
	line = line.strip()
	tok = 'FLOOR INVERSE: '
	L = len(tok)
	if line.startswith(tok):
		save_y_chart(line[L:], 'golden/golden-%08d.png'%M)
		M+=1