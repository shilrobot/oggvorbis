import numpy as np
from math import sin,pi
import matplotlib.pyplot as plt

bs0 = 256
bs1 = 1024

def vorbiswindow(j,K):
	if j < 0:
		return 0
	elif j >= K:
		return 0
	z = sin(pi/K * (j+0.5))
	return sin(pi*0.5*z*z)
	
blocks = [bs0, bs1, bs1, bs0, bs0, bs1, bs1, bs0]

def render_window(K, start, end):
	data = np.zeros(end-start)
	for j in range(end-start):
		data[j] = vorbiswindow(start+j,K)
	return data

t = 0

for i,bs in enumerate(blocks):
	leftBS = blocks[0]
	rightBS = blocks[-1]
	if i > 0:
		leftBS = blocks[i-1]
	if i < len(blocks)-1:
		rightBS = blocks[i+1]
	
	n = bs
	window_center = n/2
	
	if n == bs1 and leftBS != bs1:
		left_window_start = n/4 - bs0/4
		left_window_end = n/4 + bs0/4
		left_n = bs0/2
	else:
		left_window_start = 0
		left_window_end = window_center
		left_n = n/2
		
	if n == bs1 and rightBS != bs1:
		right_window_start = n*3/4 - bs0/4
		right_window_end = n*3/4 + bs0/4
		right_n = bs0/2
	else:
		right_window_start = window_center
		right_window_end = n
		right_n = n/2
		
	block = np.zeros(bs)
	block[0:left_window_start] = 0
	block[left_window_start:left_window_end] = render_window(left_n*2, 0, left_n)
	block[left_window_end:right_window_start] = 1
	block[right_window_start:right_window_end] = render_window(right_n*2, right_n, right_n*2)
	block[right_window_end:] = 0
		
	t -= left_window_start
	plt.plot(np.arange(0,bs)+t, block)
	t += right_window_start
	
	#plt.plot(output)
	#plt.show()
	#import sys
	#sys.exit(1)
	#render_window(0, K, 
	
plt.show()