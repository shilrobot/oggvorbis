
#f1 = open('goldenfloors.txt','rt')
#f2 = open('out.txt','rt')
import math
def seq(fn):
	for l in open(fn,'rt'):
		l = l.strip()
		if l == '':
			continue
		tok ='RESIDUE INVERSE: '
		if l.startswith(tok):
			#print '!'
			l = l[len(tok):]
			data = [float(x) for x in l.split(',')]
			yield data
	print 'Exhausted %s' % fn
import itertools
errors = []
n=0
import matplotlib.pyplot as plt
for (golden, mine) in itertools.izip(seq('goldenfloors.txt'), itertools.islice(seq('out.txt'),0,999999999)):
	#print 'n=%d'%n
	print 'DIFF: %d'%(len(mine)-len(golden))
	mine = [x*len(mine)/4.0 for x in mine]
	#plt.plot(golden,color='g')
	#plt.plot(mine,color='r')
	#plt.plot([abs(x-y) for (x,y) in zip(golden,mine)],color='b')
	#plt.savefig('out/out%08d.png'%n, dpi=50)
	#plt.clf()
	#if n > 1000:
	#	break
	n+=1
	err = 0
	for (x,y) in zip(golden,mine):
		err = max(err,abs(x-y))
	#print sse
	#print err
	errors.append(err)
	#errors.append(err)
	#print golden
	#print mine
	#print '---'
plt.plot(errors)
plt.show()